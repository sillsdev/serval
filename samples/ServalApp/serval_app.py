import streamlit as st
from serval_client_module import *
from serval_auth_module import *
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from db import Build
from time import sleep

serval_auth = None
if not st.session_state.get('authorized',False):
    with st.form(key="Authorization Form"):
        st.session_state['client_id'] = st.text_input(label='Client ID')
        st.session_state['client_secret'] = st.text_input(label='Client Secret', type='password')
        if st.form_submit_button("Authorize"):
            st.session_state['authorized'] = True
            st.rerun()
        if st.session_state.get('authorization_failure', False):
            st.error('Invalid credentials. Please check your credentials.')
else:
    try:
        serval_auth = ServalBearerAuth(client_id=st.session_state['client_id'] if st.session_state['client_id'] != "" else "<invalid>", client_secret=st.session_state['client_secret'] if st.session_state['client_secret'] != "" else "<invalid>")
    except ValueError:
        st.session_state['authorized'] = False
        st.session_state['authorization_failure'] = True
        st.rerun()
    client = RemoteCaller(url_prefix="http://localhost",auth=serval_auth)
    engine = create_engine("sqlite:///builds.db")
    Session = sessionmaker(bind=engine)
    session = Session()

    def submit():
        engine = json.loads(client.translation_engines_create(TranslationEngineConfig(source_language=st.session_state['source_language'],target_language=st.session_state['target_language'],type='Nmt',name=f'serval_app_engine:{st.session_state["email"]}')))
        source_file = json.loads(client.data_files_create(st.session_state['source_file'], format="Text"))
        target_file = json.loads(client.data_files_create(st.session_state['target_file'], format="Text"))
        corpus = json.loads(client.translation_engines_add_corpus(
            engine['id'],
            TranslationCorpusConfig(
                source_files=[TranslationCorpusFileConfig(file_id=source_file['id'], text_id=st.session_state['source_file'].name)],
                target_files=[TranslationCorpusFileConfig(file_id=target_file['id'], text_id=st.session_state['source_file'].name)],
                source_language=st.session_state['source_language'],
                target_language=st.session_state['target_language']
                )
            )
        )
        build = json.loads(client.translation_engines_start_build(engine['id'], TranslationBuildConfig(pretranslate=[PretranslateCorpusConfig(corpus_id=corpus["id"], text_ids=[st.session_state['source_file'].name])])))
        session.add(Build(build_id=build['id'],engine_id=engine['id'],email=st.session_state['email'],state=build['state'],corpus_id=corpus['id']))
        session.commit()

    def already_active_build_for(email:str):
        return len(session.query(Build).where(Build.email == email).all()) > 0

    def is_valid_passcode(passcode:str):
        return passcode == os.environ.get('SERVAL_APP_PASSCODE')

    st.subheader("Neural Machine Translation")

    tried_to_submit = st.session_state.get('tried_to_submit', False)
    with st.form(key="NmtTranslationForm"):
        st.session_state['source_language'] = st.text_input(label="Source language tag*", placeholder="en")
        if st.session_state.get('source_language','') == '' and tried_to_submit:
            st.warning("Please enter a source language tag before submitting", icon='⬆️')

        st.session_state['source_file'] = st.file_uploader(label="Source File")
        if st.session_state.get('source_file',None) is None and tried_to_submit:
            st.warning("Please upload a source file before submitting", icon='⬆️')

        st.session_state['target_language'] = st.text_input(label="Target language tag*", placeholder="es")
        if st.session_state.get('target_language','') == '' and tried_to_submit:
            st.warning("Please enter a target language tag before submitting", icon='⬆️')

        st.session_state['target_file'] = st.file_uploader(label="Target File")
        if st.session_state.get('target_file',None) is None and tried_to_submit:
            st.warning("Please upload a target file before submitting", icon='⬆️')

        st.session_state['email'] = st.text_input(label="Email", placeholder="johndoe@example.com")
        if st.session_state.get('email','') == '' and tried_to_submit:
            st.warning("Please enter an email address", icon='⬆️')
        if tried_to_submit:
            st.error(st.session_state.get('error',"Something went wrong. Please try again in a moment."))
        if st.form_submit_button("Generate translations"):
            if already_active_build_for(st.session_state['email']):
                st.session_state['tried_to_submit'] = True
                st.session_state['error'] = "There is already an a pending or active build associated with this email address. Please wait for the previous build to finish."
                st.rerun()
            elif st.session_state['source_language'] != '' and st.session_state['target_language'] != '' and st.session_state['source_file'] is not None and st.session_state['target_file'] is not None and st.session_state['email'] != '':
                submit()
                st.session_state['tried_to_submit'] = False
                st.toast("Translations are on their way! You'll receive an email when your translation job has begun.")
                sleep(4)
                st.rerun()
            else:
                st.session_state['tried_to_submit'] = True
                st.session_state['error'] = "Some required fields were left blank. Please fill in all fields above"
                st.rerun()
        st.markdown("<sub>\* Use IETF tags if possible. See [here](https://en.wikipedia.org/wiki/IETF_language_tag) for more information on IETF tags.</sub>", unsafe_allow_html=True)