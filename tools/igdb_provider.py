"""Optional GridLine IGDB metadata bridge. Listens only on this PC."""

import datetime
import functools
import http.server
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request


class NoRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


HTTP = urllib.request.build_opener(NoRedirects)
MAX_RESPONSE = 1024 * 1024


def request_json(url, body, headers):
    request = urllib.request.Request(url, data=body.encode("utf-8"), headers=headers)
    with HTTP.open(request, timeout=7) as response:
        payload = response.read(MAX_RESPONSE + 1)
    if len(payload) > MAX_RESPONSE:
        raise ValueError("Provider response exceeds the local limit")
    return json.loads(payload)


class Igdb:
    def __init__(self, client, secret):
        self.client = client
        self.secret = secret
        self.token = None
        self.expires = 0
        self.last_query = 0

    def access_token(self):
        if not self.token or time.monotonic() >= self.expires:
            body = urllib.parse.urlencode({
                "client_id": self.client, "client_secret": self.secret,
                "grant_type": "client_credentials",
            })
            reply = request_json("https://id.twitch.tv/oauth2/token", body, {
                "Content-Type": "application/x-www-form-urlencoded",
            })
            self.token = reply["access_token"]
            self.expires = time.monotonic() + max(0, int(reply["expires_in"]) - 60)
        return self.token

    def query(self, endpoint, body):
        time.sleep(max(0, self.last_query + .26 - time.monotonic()))
        self.last_query = time.monotonic()
        try:
            return request_json("https://api.igdb.com/v4/" + endpoint, body, {
                "Client-ID": self.client,
                "Authorization": "Bearer " + self.access_token(),
                "Accept": "application/json",
                "Content-Type": "text/plain; charset=utf-8",
            })
        except urllib.error.HTTPError as error:
            if error.code == 401:
                self.token = None
            raise

    @functools.lru_cache(maxsize=1)
    def steam_source(self):
        results = self.query("external_game_sources", 'fields id; where name = "Steam"; limit 2;')
        return int(results[0]["id"]) if len(results) == 1 else None

    @functools.lru_cache(maxsize=256)
    def metadata(self, appid, cache_week):
        source = self.steam_source()
        if source is None:
            return None
        results = self.query("external_games",
            "fields game.id,game.summary,game.first_release_date,game.involved_companies.developer,"
            "game.involved_companies.publisher,game.involved_companies.company.name;"
            f' where external_game_source = {source} & uid = "{appid}"; limit 10;')
        matches = {entry["game"]["id"]: entry["game"] for entry in results if isinstance(entry.get("game"), dict)}
        if len(matches) != 1:
            return None
        game = next(iter(matches.values()))
        summary = game.get("summary")
        companies = game.get("involved_companies", [])

        def names(role):
            values = (entry.get("company", {}).get("name") for entry in companies if entry.get(role))
            return ", ".join(dict.fromkeys(value for value in values if value))[:300] or None

        stamp = game.get("first_release_date")
        release = datetime.datetime.fromtimestamp(stamp, datetime.timezone.utc).strftime("%Y") if stamp else None
        return {
            "Summary": summary[:1200] if summary else None,
            "Developer": names("developer"), "Publisher": names("publisher"), "Release": release,
        }


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        match = re.fullmatch(r"/games/([1-9][0-9]{0,9})", self.path)
        if not match or int(match[1]) > 4294967295:
            self.send_error(404)
            return
        try:
            result = self.server.provider.metadata(match[1], int(time.time() // 604800))
        except urllib.error.HTTPError as error:
            self.send_error(429 if error.code == 429 else 503)
            return
        except (OSError, ValueError, KeyError, TypeError):
            self.send_error(503)
            return
        if result is None:
            self.send_error(404)
            return
        payload = json.dumps(result).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, format, *args):
        pass


def main():
    client = os.environ.get("GRIDLINE_IGDB_CLIENT_ID")
    secret = os.environ.get("GRIDLINE_IGDB_CLIENT_SECRET")
    if not client or not secret:
        raise SystemExit("Set GRIDLINE_IGDB_CLIENT_ID and GRIDLINE_IGDB_CLIENT_SECRET before starting the bridge.")
    with http.server.HTTPServer(("127.0.0.1", 8766), Handler) as server:
        server.provider = Igdb(client, secret)
        print("GridLine IGDB metadata bridge listening on 127.0.0.1:8766", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
