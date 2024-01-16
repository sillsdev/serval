import json
import os
import time

import requests


class ServalBearerAuth(requests.auth.AuthBase):
    def __init__(self, client_id="", client_secret=""):
        self.__client_id = (
            client_id if client_id != "" else os.environ.get("SERVAL_CLIENT_ID")
        )
        assert self.__client_id is not None
        self.__client_secret = (
            client_secret
            if client_secret != ""
            else os.environ.get("SERVAL_CLIENT_SECRET")
        )
        assert self.__client_secret is not None
        self.__auth_url = os.environ.get("SERVAL_AUTH_URL")
        assert self.__auth_url is not None
        self.update_token()
        self.__last_time_fetched = time.time()

    def __call__(self, r):
        if time.time() - self.__last_time_fetched > 20 * 60:
            self.update_token()
        r.headers["authorization"] = "Bearer " + self.token
        return r

    def update_token(self):
        data = {
            "client_id": f"{self.__client_id}",
            "client_secret": f"{self.__client_secret}",
            "audience": "https://serval-api.org/",
            "grant_type": "client_credentials",
        }

        encoded_data = json.dumps(data).encode("utf-8")
        r = None
        try:
            r: requests.Response = requests.post(
                url=f"{self.__auth_url}/oauth/token",
                data=encoded_data,
                headers={"content-type": "application/json"},
            )
            self.token = r.json()["access_token"] if r is not None else None
        except Exception as e:
            raise ValueError(
                f"Token cannot be None. Failed to retrieve token from auth server; responded \
                    with {r.status_code if r is not None else '<unknown>'}. Original exception: {e}"
            )
