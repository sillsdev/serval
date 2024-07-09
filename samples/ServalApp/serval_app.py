import json
import os
import re
from time import sleep

import streamlit as st

st.set_page_config(layout="wide")
from db import Build, State, create_db_if_not_exists
from serval_auth_module import ServalBearerAuth
from serval_client_module import (
    PretranslateCorpusConfig,
    TrainingCorpusConfig,
    RemoteCaller,
    TranslationBuildConfig,
    TranslationCorpusConfig,
    TranslationCorpusFileConfig,
    TranslationEngineConfig,
)
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from streamlit.logger import get_logger, set_log_level

create_db_if_not_exists()

set_log_level("INFO")
logger = get_logger(__name__)

engine = create_engine("sqlite:///builds.db")
Session = sessionmaker(bind=engine)
session = Session()

if not st.session_state.get("authorized", False):
    with st.form(key="Authorization Form"):
        st.session_state.client_id = st.text_input(label="Client ID")
        st.session_state.client_secret = st.text_input(
            label="Client Secret", type="password"
        )
        if st.form_submit_button("Authorize"):
            try:
                st.session_state.serval_auth = ServalBearerAuth(
                    client_id=(
                        st.session_state.client_id
                        if st.session_state.client_id != ""
                        else "<invalid>"
                    ),
                    client_secret=(
                        st.session_state.client_secret
                        if st.session_state.client_secret != ""
                        else "<invalid>"
                    ),
                )
                st.session_state.authorized = True
                st.rerun()
            except ValueError:
                st.session_state.authorized = False
                st.session_state.authorization_failure = True
        if st.session_state.get("authorization_failure", False):
            st.error("Invalid credentials. Please check your credentials.")
else:
    client = RemoteCaller(
        url_prefix=os.environ.get("SERVAL_HOST_URL"),
        auth=st.session_state.serval_auth,
    )

    def refresh_builds():
        def started(build: Build, data=None):
            logger.info(f"Started:\n{build}")
            session.delete(build)
            session.add(
                Build(
                    build_id=build.build_id,
                    engine_id=build.engine_id,
                    state=State.Active,
                    corpus_id=build.corpus_id,
                )
            )
            session.commit()

        logger.info("Build updated.")

        def faulted(build: Build, data=None):
            logger.warn(f"Faulted:\n{build}\n{data}")
            session.delete(build)
            session.add(
                Build(
                    build_id=build.build_id,
                    engine_id=build.engine_id,
                    state=State.Faulted,
                    corpus_id=build.corpus_id,
                )
            )
            session.commit()
            logger.info("Build deleted.")

        def completed(build: Build, data=None):
            logger.info(f"Completed:\{build}")
            session.delete(build)
            session.add(
                Build(
                    build_id=build.build_id,
                    engine_id=build.engine_id,
                    state=State.Completed,
                    corpus_id=build.corpus_id,
                )
            )
            session.commit()

        def default_update(build: Build, data=None):
            logger.info(f"Updated:\n{build}")

        responses: "dict[str,function]" = {
            "Completed": completed,
            "Faulted": faulted,
            "Canceled": faulted,
        }

        def get_update(build: Build):
            build_update = client.translation_engines_get_build(
                id=build.engine_id, build_id=build.build_id
            )
            if build.state == State.Pending and build_update.state == "Active":
                started(build)
            else:
                responses.get(build_update.state, default_update)(
                    build, build_update.message
                )

        logger.info("Checking for updates...")
        with session.no_autoflush:
            builds = (
                session.query(Build)
                .where(Build.client_id == st.session_state.serval_auth.client_id)
                .all()
            )
            for build in builds:
                try:
                    get_update(build)
                except Exception as e:
                    logger.error(f"Failed to update {build} because of exception {e}")
                    raise e
            st.session_state.builds = builds

    refresh_builds()

    def submit():
        engine = json.loads(
            client.translation_engines_create(
                TranslationEngineConfig(
                    source_language=st.session_state.source_language,
                    target_language=st.session_state.target_language,
                    type="Nmt",
                    name=(
                        st.session_state.build_name
                        if "build_name" in st.session_state
                        else f"serval_app_engine:{st.session_state.client_id}"
                    ),
                    is_model_persisted=st.session_state.persist_model,
                )
            )
        )
        source_files = [
            json.loads(
                client.data_files_create(
                    st.session_state.source_files[i],
                    format=(
                        "Paratext"
                        if st.session_state.source_files[i].name[-4:]
                        in [".zip", ".tar", "r.gz"]
                        else "Text"
                    ),
                )
            )
            for i in range(len(st.session_state.source_files))
        ]
        target_files = [
            json.loads(
                client.data_files_create(
                    st.session_state.target_files[i],
                    format=(
                        "Paratext"
                        if st.session_state.target_files[i].name[-4:] == ".zip"
                        else "Text"
                    ),
                )
            )
            for i in range(len(st.session_state.target_files))
        ]
        corpus = json.loads(
            client.translation_engines_add_corpus(
                engine["id"],
                TranslationCorpusConfig(
                    source_files=[
                        TranslationCorpusFileConfig(file_id=file["id"], text_id=name)
                        for file, name in zip(
                            source_files,
                            list(map(lambda f: f.name, st.session_state.source_files)),
                        )
                    ],
                    target_files=[
                        TranslationCorpusFileConfig(file_id=file["id"], text_id=name)
                        for file, name in zip(
                            target_files,
                            list(map(lambda f: f.name, st.session_state.target_files)),
                        )
                    ],
                    source_language=st.session_state.source_language,
                    target_language=st.session_state.target_language,
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
                            text_ids=(
                                []
                                if st.session_state.source_files[0].name[-4:] == ".zip"
                                else list(
                                    map(
                                        lambda f: f.name,
                                        st.session_state.source_files,
                                    )
                                )
                            ),
                        ),
                    ],
                    train_on=[
                        TrainingCorpusConfig(
                            corpus_id=corpus["id"],
                            text_ids=(
                                []
                                if st.session_state.source_files[0].name[-4:] == ".zip"
                                else list(
                                    map(
                                        lambda f: f.name,
                                        st.session_state.source_files,
                                    )
                                )
                            ),
                        )
                    ],
                    options='{"max_steps":'
                    + str(os.environ.get("SERVAL_APP_MAX_STEPS", 20_000))
                    + "}",
                    name=(
                        st.session_state.build_name
                        if "build_name" in st.session_state
                        else f"serval_app_engine:{st.session_state.client_id}"
                    ),
                ),
            )
        )
        session.add(
            Build(
                build_id=build["id"],
                engine_id=engine["id"],
                state=build["state"],
                corpus_id=corpus["id"],
                client_id=st.session_state.client_id,
                source_files=", ".join(
                    list(map(lambda f: f.name, st.session_state.source_files))
                ),
                target_files=", ".join(
                    list(map(lambda f: f.name, st.session_state.target_files))
                ),
                name=st.session_state.build_name,
                is_model_persisted=engine["isModelPersisted"],
            )
        )
        session.commit()

    def delete_build_by_id(id: str) -> bool:
        try:
            build = session.query(Build).where(Build.engine_id == id).one()
            ret = True
            if build.state == State.Active or build.state == State.Pending:
                ret = cancel_build_by_id(id)
            session.delete(build)
            session.commit()
            refresh_builds()
            return ret
        except Exception as e:
            logger.warn(e.with_traceback())

    def cancel_build_by_id(id: str) -> bool:
        try:
            client.translation_engines_cancel_build(id)
            refresh_builds()
            return delete_build_by_id(id)
        except Exception as e:
            logger.warn(e.with_traceback())
            return False

    def get_download_url_for_build_by_id(id: str) -> str:
        ret = client.translation_engines_get_model_download_url(id)
        return ret.url

    def get_pretranslations_for_build_by_id(id: str) -> str:
        build = session.query(Build).where(Build.engine_id == id).one()
        try:
            pretranslations = client.translation_engines_get_all_pretranslations(
                build.engine_id, build.corpus_id
            )
            return "\n".join(
                [
                    f"{'|'.join(pretranslation.refs)}\t{pretranslation.translation}"
                    for pretranslation in pretranslations
                ]
            )
        except:
            return ""

    def number_of_active_builds_for(client: str):
        active_builds = (
            session.query(Build)
            .where(Build.client_id == client)
            .where(Build.state == State.Pending or Build.state == State.Active)
            .all()
        )
        logger.info(active_builds)
        return len(active_builds)

    st.subheader("Neural Machine Translation")
    st.markdown("<h5>Start a new build</h5>", unsafe_allow_html=True)
    tried_to_submit = st.session_state.get("tried_to_submit", False)
    submitted = False
    with st.form(key="NmtTranslationForm"):
        st.session_state.build_name = st.text_input(
            label="Build Name", placeholder="MyBuild (Optional)"
        )
        st.session_state.source_language = st.text_input(
            label="Source language tag*", placeholder="en"
        )
        if st.session_state.get("source_language", "") == "" and tried_to_submit:
            st.error("Please enter a source language tag before submitting", icon="⬆️")

        st.session_state.source_files = st.file_uploader(
            label="Source File(s)", accept_multiple_files=True
        )
        if len(st.session_state.get("source_files", [])) == 0 and tried_to_submit:
            st.error("Please upload a source file before submitting", icon="⬆️")
        if len(st.session_state.get("source_files", [])) > 1:
            st.warning(
                "Please note that source and target text files will be paired together by file name",
                icon="💡",
            )

        st.session_state.target_language = st.text_input(
            label="Target language tag*", placeholder="es"
        )
        if st.session_state.get("target_language", "") == "" and tried_to_submit:
            st.error("Please enter a target language tag before submitting", icon="⬆️")

        st.session_state.target_files = st.file_uploader(
            label="Target File(s)", accept_multiple_files=True
        )
        if len(st.session_state.get("target_files", [])) > 1:
            st.warning(
                "Please note that source and target text files will be paired together by file name",
                icon="💡",
            )
        st.session_state.persist_model = st.toggle(
            label="Save fine-tuned model for downloading?"
        )

        if tried_to_submit:
            st.error(
                st.session_state.get(
                    "error", "Something went wrong. Please try again in a moment."
                )
            )
        if st.form_submit_button("Generate translations"):
            if number_of_active_builds_for(st.session_state.client_id) >= 3:
                st.session_state.tried_to_submit = True
                st.session_state.error = "There is already an a pending or active build associated with this client id. \
                        Please wait for the previous build to finish."
            elif (
                st.session_state.source_language != ""
                and st.session_state.target_language != ""
                and len(st.session_state.source_files) > 0
            ):
                with st.spinner():
                    submit()
                st.session_state.tried_to_submit = False
                st.toast(
                    "Translations are on their way! Refresh occasionally to see if your build is complete (Builds typically take around 8 hours to run; there may be additional delays due to long queues)"
                )
                st.session_state.error = None
                sleep(4)
                st.rerun()
            else:
                st.session_state.tried_to_submit = True
                st.session_state.error = "Some required fields were left blank. Please fill in all fields above"
        st.markdown(
            f"<sub>\* Use IETF tags if possible. See [here](https://en.wikipedia.org/wiki/IETF_language_tag) \
                for more information on IETF tags. For more details, see [the Serval API documentation]({os.environ.get('SERVAL_HOST_URL')}/swagger/index.html#/Translation%20Engines/TranslationEngines_Create).</sub>",
            unsafe_allow_html=True,
        )

    st.divider()

    st.markdown("<h5>Your active/completed builds</h5>", unsafe_allow_html=True)
    if st.button(label="Refresh"):
        refresh_builds()
    with st.container(border=True):
        c1, c2, c3, c4, c5, c6 = st.columns([1, 1, 1, 1, 2, 2])
        with c1:
            st.write("Name")
        with c2:
            st.write("Status")
        for build in st.session_state.builds:
            with st.container(border=True):
                c1, c2, c3, c4, c5, c6 = st.columns([1, 1, 1, 1, 2, 2])
                with c1:
                    st.write(
                        f"<h5>{build.name}</h5>",
                        unsafe_allow_html=True,
                    )
                with c2:
                    st.write(
                        f"<h5>{build.state.name}</h5>",
                        unsafe_allow_html=True,
                    )
                with c3:
                    if st.button("Delete", key=f"delete_{build.build_id}"):
                        delete_build_by_id(build.engine_id)
                with c4:
                    if st.button("Cancel", key=f"cancel_{build.build_id}"):
                        cancel_build_by_id(build.engine_id)
                with c5:
                    get_pretranslations_is_disabled = build.state != State.Completed
                    st.download_button(
                        "Download Translations",
                        key=f"download_translations_{build.build_id}",
                        data=(
                            get_pretranslations_for_build_by_id(build.engine_id)
                            if not get_pretranslations_is_disabled
                            else ""
                        ),
                        disabled=get_pretranslations_is_disabled,
                    )
                with c6:
                    download_model_is_disabled = (
                        build.state != State.Completed or not build.is_model_persisted
                    )
                    st.link_button(
                        url=(
                            get_download_url_for_build_by_id(build.engine_id)
                            if not download_model_is_disabled
                            else "https://localhost"
                        ),
                        label="Download Model",
                        disabled=download_model_is_disabled,
                    )

    if len(st.session_state.builds) == 0:
        st.write("(No active or completed builds)")
