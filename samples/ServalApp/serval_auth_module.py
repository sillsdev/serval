import json
import os
import time
from datetime import datetime, timedelta

import requests

from db import AuthToken

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from streamlit.logger import get_logger
from logging import Logger


class ServalBearerAuth(requests.auth.AuthBase):
    def __init__(self, client_id="", client_secret=""):
        self.client_id = (
            client_id if client_id != "" else os.environ.get("SERVAL_CLIENT_ID")
        )
        assert self.client_id is not None
        self.__client_secret = (
            client_secret
            if client_secret != ""
            else os.environ.get("SERVAL_CLIENT_SECRET")
        )
        assert self.__client_secret is not None
        self.__auth_url = os.environ.get("SERVAL_AUTH_URL")
        assert self.__auth_url is not None
        self.logger: Logger = get_logger(__name__)
        self.token = None
        self.__last_time_fetched = 0
        self.get_token()

    def __call__(self, r):
        self.get_token()
        r.headers["authorization"] = "Bearer " + self.token
        return r

    def get_token(self):
        if self.token and (time.time() - self.__last_time_fetched) < 20 * 60:
            return
        engine = create_engine("sqlite:///builds.db")
        Session = sessionmaker(bind=engine)
        session = Session()
        tokens = (
            session.query(AuthToken).where(AuthToken.client_id == self.client_id).all()
        )
        session.close()
        if len(tokens) > 0:
            current_token = tokens[0]
            if (current_token.exp_date + timedelta(minutes=20)) >= datetime.now():
                self.logger.info(
                    f"Using unexpired token with expiration date {current_token.exp_date}"
                )
                self.token = current_token.token
                return
        data = {
            "client_id": f"{self.client_id}",
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
            expires_in = int(r.json()["expires_in"] if r is not None else 0)
            engine = create_engine("sqlite:///builds.db")
            Session = sessionmaker(bind=engine)
            session = Session()
            tokens = (
                session.query(AuthToken)
                .where(AuthToken.client_id == self.client_id)
                .all()
            )
            if len(tokens) > 0:
                session.delete(tokens[0])
            session.add(
                AuthToken(
                    client_id=self.client_id,
                    token=self.token,
                    exp_date=datetime.now() + timedelta(seconds=expires_in),
                )
            )
            session.commit()
            self.__last_time_fetched = time.time()
            self.logger.info(f"Getting new token; expires in {expires_in} seconds")
        except Exception as e:
            raise ValueError(
                f"Token cannot be None. Failed to retrieve token from auth server; responded \
                    with {r.status_code if r is not None else '<unknown>'}. Original exception: {e}"
            )
