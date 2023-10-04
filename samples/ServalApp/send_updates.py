from serval_client_module import *
from serval_auth_module import *
import os
from time import sleep
from db import Build, State
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from serval_email_module import ServalAppEmailServer

def main():
    def started(build:Build, email_server:ServalAppEmailServer):
        print(f"\tStarted {build}")
        session.delete(build)
        email_server.send_build_started_email(build.email)
        session.add(Build(build_id=build.build_id, engine_id=build.engine_id, email=build.email, state=State.Active, corpus_id=build.corpus_id))

    def faulted(build:Build, email_server:ServalAppEmailServer):
        print(f"\tFaulted {build}")
        session.delete(build)
        email_server.send_build_faulted_email(build.email)

    def completed(build:Build, email_server:ServalAppEmailServer):
        print(f"\tCompleted {build}")
        session.delete(build)
        pretranslations = client.translation_engines_get_all_pretranslations(build.engine_id, build.corpus_id)
        email_server.send_build_completed_email(build.email, '\n'.join([f"{'|'.join(pretranslation.refs)}\t{pretranslation.translation}" for pretranslation in pretranslations]))

    def update(build:Build, email_server:ServalAppEmailServer):
        print(f"\tUpdated {build}")

    serval_auth = ServalBearerAuth()
    client = RemoteCaller(url_prefix="http://localhost",auth=serval_auth)
    responses:"dict[str,function]" = {"Completed":completed, "Faulted":faulted, "Canceled":faulted}

    engine = create_engine("sqlite:///builds.db")
    Session = sessionmaker(bind=engine)
    session = Session()

    def get_update(build:Build, email_server:ServalAppEmailServer):
        build_update = client.translation_engines_get_build(id=build.engine_id, build_id=build.build_id)
        if build.state == State.Pending and build_update.state == "Active":
            started(build, email_server)
        else:
            responses.get(build_update.state, update)(build, email_server)
        session.commit()

    def send_updates(email_server:ServalAppEmailServer):
        print(f"Checking for updates:")
        builds = session.query(Build).all()
        for build in builds:
            try:
                get_update(build, email_server)
            except Exception as e:
                print(f"\tFailed to update {build} because of exception {e}")
        sleep(60)

    with ServalAppEmailServer(os.environ.get('SERVAL_APP_EMAIL_PASSWORD')) as email_server:
        while(True):
            send_updates(email_server)

if __name__ == "__main__":
    main()