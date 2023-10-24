import json
import os
import re
from threading import Thread
from time import sleep

import streamlit as st
from db import Build, State, create_db_if_not_exists
from serval_auth_module import ServalBearerAuth
from serval_client_module import (
    PretranslateCorpusConfig,
    RemoteCaller,
    TranslationBuildConfig,
    TranslationCorpusConfig,
    TranslationCorpusFileConfig,
    TranslationEngineConfig,
)
from serval_email_module import ServalAppEmailServer
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from streamlit.logger import get_logger, set_log_level
from streamlit.runtime.scriptrunner import add_script_run_ctx

create_db_if_not_exists()

set_log_level("INFO")
logger = get_logger(__name__)


def send_emails():
    engine = create_engine("sqlite:///builds.db")
    Session = sessionmaker(bind=engine)
    session = Session()
    try:

        def started(build: Build, email_server: ServalAppEmailServer, data=None):
            logger.info(f"Started:\n{build}")
            email_server.send_build_started_email(build.email, str(build))
            session.delete(build)
            session.add(
                Build(
                    build_id=build.build_id,
                    engine_id=build.engine_id,
                    email=build.email,
                    state=State.Active,
                    corpus_id=build.corpus_id,
                )
            )

        def faulted(build: Build, email_server: ServalAppEmailServer, data=None):
            logger.warn(f"Faulted:\n{build}")
            email_server.send_build_faulted_email(build.email, str(build), error=data)
            session.delete(build)

        def completed(build: Build, email_server: ServalAppEmailServer, data=None):
            logger.info(f"Completed:\{build}")
            pretranslations = client.translation_engines_get_all_pretranslations(
                build.engine_id, build.corpus_id
            )
            email_server.send_build_completed_email(
                build.email,
                "\n".join(
                    [
                        f"{'|'.join(pretranslation.refs)}\t{pretranslation.translation}"
                        for pretranslation in pretranslations
                    ]
                ),
                str(build),
            )
            session.delete(build)

        def default_update(build: Build, email_server: ServalAppEmailServer, data=None):
            logger.info(f"Updated:\n{build}")

        serval_auth = ServalBearerAuth()
        client = RemoteCaller(
            url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth
        )
        responses: "dict[str,function]" = {
            "Completed": completed,
            "Faulted": faulted,
            "Canceled": faulted,
        }

        def get_update(build: Build, email_server: ServalAppEmailServer):
            build_update = client.translation_engines_get_build(
                id=build.engine_id, build_id=build.build_id
            )
            if build.state == State.Pending and build_update.state == "Active":
                started(build, email_server)
            else:
                responses.get(build_update.state, default_update)(
                    build, email_server, build_update.message
                )
            session.commit()

        def send_updates(email_server: ServalAppEmailServer):
            logger.info("Checking for updates...")
            with session.no_autoflush:
                builds = session.query(Build).all()
                for build in builds:
                    try:
                        get_update(build, email_server)
                    except Exception as e:
                        logger.error(
                            f"Failed to update {build} because of exception {e}"
                        )
                        raise e

        with ServalAppEmailServer(
            os.environ.get("SERVAL_APP_EMAIL_PASSWORD")
        ) as email_server:
            while True:
                send_updates(email_server)
                sleep(int(os.environ.get("SERVAL_APP_UPDATE_FREQ_SEC", 300)))
    except Exception as e:
        logger.exception(e)
        st.session_state["background_process_has_started"] = False


if not st.session_state.get("background_process_has_started", False):
    cron_thread = Thread(target=send_emails)
    add_script_run_ctx(cron_thread)
    cron_thread.start()
    st.session_state["background_process_has_started"] = True

serval_auth = None
if not st.session_state.get("authorized", False):
    with st.form(key="Authorization Form"):
        st.session_state["client_id"] = st.text_input(label="Client ID")
        st.session_state["client_secret"] = st.text_input(
            label="Client Secret", type="password"
        )
        if st.form_submit_button("Authorize"):
            st.session_state["authorized"] = True
            st.rerun()
        if st.session_state.get("authorization_failure", False):
            st.error("Invalid credentials. Please check your credentials.")
else:
    try:
        serval_auth = ServalBearerAuth(
            client_id=st.session_state["client_id"]
            if st.session_state["client_id"] != ""
            else "<invalid>",
            client_secret=st.session_state["client_secret"]
            if st.session_state["client_secret"] != ""
            else "<invalid>",
        )
    except ValueError:
        st.session_state["authorized"] = False
        st.session_state["authorization_failure"] = True
        st.rerun()
    client = RemoteCaller(
        url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth
    )
    engine = create_engine("sqlite:///builds.db")
    Session = sessionmaker(bind=engine)
    session = Session()

    def submit():
        engine = json.loads(
            client.translation_engines_create(
                TranslationEngineConfig(
                    source_language=st.session_state["source_language"],
                    target_language=st.session_state["target_language"],
                    type="Nmt",
                    name=st.session_state["build_name"]
                    if "build_name" in st.session_state
                    else f'serval_app_engine:{st.session_state["email"]}',
                )
            )
        )
        source_files = [
            json.loads(
                client.data_files_create(
                    st.session_state["source_files"][i],
                    format="Paratext"
                    if st.session_state["source_files"][i].name[-4:]
                    in [".zip", ".tar", "r.gz"]
                    else "Text",
                )
            )
            for i in range(len(st.session_state["source_files"]))
        ]
        target_files = [
            json.loads(
                client.data_files_create(
                    st.session_state["target_files"][i],
                    format="Paratext"
                    if st.session_state["target_files"][i].name[-4:] == ".zip"
                    else "Text",
                )
            )
            for i in range(len(st.session_state["target_files"]))
        ]
        corpus = json.loads(
            client.translation_engines_add_corpus(
                engine["id"],
                TranslationCorpusConfig(
                    source_files=[
                        TranslationCorpusFileConfig(file_id=file["id"], text_id=name)
                        for file, name in zip(
                            source_files,
                            list(
                                map(lambda f: f.name, st.session_state["source_files"])
                            ),
                        )
                    ],
                    target_files=[
                        TranslationCorpusFileConfig(file_id=file["id"], text_id=name)
                        for file, name in zip(
                            target_files,
                            list(
                                map(lambda f: f.name, st.session_state["target_files"])
                            ),
                        )
                    ],
                    source_language=st.session_state["source_language"],
                    target_language=st.session_state["target_language"],
                ),
            )
        )
        build = json.loads(
            client.translation_engines_start_build(
                engine["id"],
                TranslationBuildConfig(
                    pretranslate=[
                        PretranslateCorpusConfig(
                            corpus_id=corpus["id"],
                            text_ids=[]
                            if st.session_state["source_files"][0].name[-4:] == ".zip"
                            else list(
                                map(lambda f: f.name, st.session_state["source_files"])
                            ),
                        )
                    ],
                    options='{"max_steps":'
                    + str(os.environ.get("SERVAL_APP_MAX_STEPS", 10))
                    + "}",
                    name=st.session_state["build_name"]
                    if "build_name" in st.session_state
                    else f'serval_app_engine:{st.session_state["email"]}',
                ),
            )
        )
        session.add(
            Build(
                build_id=build["id"],
                engine_id=engine["id"],
                email=st.session_state["email"],
                state=build["state"],
                corpus_id=corpus["id"],
                client_id=st.session_state["client_id"],
                source_files=", ".join(
                    list(map(lambda f: f.name, st.session_state["source_files"]))
                ),
                target_files=", ".join(
                    list(map(lambda f: f.name, st.session_state["target_files"]))
                ),
                name=st.session_state["build_name"],
            )
        )
        session.commit()

    def already_active_build_for(email: str, client: str):
        return (
            len(
                session.query(Build)
                .where(Build.email == email and Build.client_id == client)
                .all()
            )
            > 0
        )

    st.subheader("Neural Machine Translation")

    tried_to_submit = st.session_state.get("tried_to_submit", False)
    with st.form(key="NmtTranslationForm"):
        st.session_state["build_name"] = st.text_input(
            label="Build Name", placeholder="MyBuild (Optional)"
        )
        st.session_state["source_language"] = st.text_input(
            label="Source language tag*", placeholder="en"
        )
        if st.session_state.get("source_language", "") == "" and tried_to_submit:
            st.error("Please enter a source language tag before submitting", icon="⬆️")

        st.session_state["source_files"] = st.file_uploader(
            label="Source File(s)", accept_multiple_files=True
        )
        if len(st.session_state.get("source_files", [])) == 0 and tried_to_submit:
            st.error("Please upload a source file before submitting", icon="⬆️")
        if len(st.session_state.get("source_files", [])) > 1:
            st.warning(
                "Please note that source and target text files will be paired together by file name",
                icon="💡",
            )

        st.session_state["target_language"] = st.text_input(
            label="Target language tag*", placeholder="es"
        )
        if st.session_state.get("target_language", "") == "" and tried_to_submit:
            st.error("Please enter a target language tag before submitting", icon="⬆️")

        st.session_state["target_files"] = st.file_uploader(
            label="Target File(s)", accept_multiple_files=True
        )
        if len(st.session_state.get("target_files", [])) > 1:
            st.warning(
                "Please note that source and target text files will be paired together by file name",
                icon="💡",
            )

        st.session_state["email"] = st.text_input(
            label="Email", placeholder="johndoe@example.com"
        )
        if st.session_state.get("email", "") == "" and tried_to_submit:
            st.error("Please enter an email address", icon="⬆️")
        elif (
            not re.match(r"^\S+@\S+\.\S+$", st.session_state["email"])
            and tried_to_submit
        ):
            st.error("Please enter a valid email address", icon="⬆️")
            st.session_state["email"] = ""
        if tried_to_submit:
            st.error(
                st.session_state.get(
                    "error", "Something went wrong. Please try again in a moment."
                )
            )
        if st.form_submit_button("Generate translations"):
            if already_active_build_for(
                st.session_state["email"], st.session_state["client_id"]
            ):
                st.session_state["tried_to_submit"] = True
                st.session_state[
                    "error"
                ] = "There is already an a pending or active build associated with this email address and client id. \
                    Please wait for the previous build to finish."
                st.rerun()
            elif (
                st.session_state["source_language"] != ""
                and st.session_state["target_language"] != ""
                and len(st.session_state["source_files"]) > 0
                and st.session_state["email"] != ""
            ):
                with st.spinner():
                    submit()
                st.session_state["tried_to_submit"] = False
                st.toast(
                    "Translations are on their way! You'll receive an email when your translation job has begun."
                )
                sleep(4)
                st.rerun()
            else:
                st.session_state["tried_to_submit"] = True
                st.session_state[
                    "error"
                ] = "Some required fields were left blank. Please fill in all fields above"
                st.rerun()
        st.markdown(
            f"<sub>\* Use IETF tags if possible. See [here](https://en.wikipedia.org/wiki/IETF_language_tag) \
                for more information on IETF tags. For more details, see [the Serval API documentation]({os.environ.get('SERVAL_HOST_URL')}/swagger/index.html#/Translation%20Engines/TranslationEngines_Create).</sub>",
            unsafe_allow_html=True,
        )
