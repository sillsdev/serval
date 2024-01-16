import argparse
import os
from serval_client_module import RemoteCaller, TranslationBuild
from serval_auth_module import ServalBearerAuth
from dateutil.parser import parse
import json
from zipfile import ZipFile


def main():
    parser = argparse.ArgumentParser(description="Zip engine data and corpora data")
    parser.add_argument("--engine-id", help="Engine id", required=True)
    parser.add_argument(
        "--build-id",
        default=None,
        help="Build id (if none is provided, the last 10 builds (or as many builds as there are if there are less than 10) are collected)",
    )
    parser.add_argument(
        "--client-id",
        default="",
        help="Serval client id (if none is provided env var SERVAL_CLIENT_ID will be used)",
    )
    parser.add_argument(
        "--client-secret",
        default="",
        help="Serval client secret (if none is provided env var SERVAL_CLIENT_SECRET will be used)",
    )
    parser.add_argument(
        "--output", default="engine_data.zip", help="Output zip filename"
    )
    args = parser.parse_args()

    serval_auth = ServalBearerAuth(
        client_id=args.client_id, client_secret=args.client_secret
    )
    client = RemoteCaller(
        url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth
    )

    engine = client.translation_engines_get(args.engine_id)
    builds: list[TranslationBuild]
    if args.build_id is None:
        builds = client.translation_engines_get_all_builds(args.engine_id)
        builds.sort(key=lambda b: parse(b.date_finished), reverse=True)
        builds = builds[: min(10, len(builds))]
    else:
        builds = [client.translation_engines_get_build(args.engine_id, args.build_id)]
    corpora = client.translation_engines_get_all_corpora(args.engine_id)
    corpora_objs = []
    with ZipFile(args.output, "w") as zip_obj:
        for corpus in corpora:
            obj = corpus.to_jsonable()
            obj["sourceFilesMeta"] = []
            for f in corpus.source_files:
                file_id = f.file.id
                file_data = client.data_files_download(file_id)
                file_meta = client.data_files_get(file_id)
                zip_obj.writestr(
                    f"{corpus.name}_{corpus.id}/src/{file_meta.name}", file_data.read()
                )
                file_meta = file_meta.to_jsonable()
                file_meta["textId"] = f.text_id
                obj["sourceFilesMeta"].append(file_meta)

            obj["targetFilesMeta"] = []
            for f in corpus.target_files:
                file_id = f.file.id
                file_data = client.data_files_download(file_id)
                file_meta = client.data_files_get(file_id)
                zip_obj.writestr(
                    f"{corpus.name}_{corpus.id}/trg/{file_meta.name}", file_data.read()
                )
                file_meta = file_meta.to_jsonable()
                file_meta["textId"] = f.text_id
                obj["targetFilesMeta"].append(file_meta)

            del obj["sourceFiles"]
            del obj["targetFiles"]

            corpora_objs.append(obj)

        meta = {}
        meta["engineMeta"] = engine.to_jsonable()
        meta["builds"] = list(map(lambda b: b.to_jsonable(), builds))
        meta["corpora"] = corpora_objs
        zip_obj.writestr(f"engine_meta.json", json.dumps(meta, indent=1))


if __name__ == "__main__":
    main()
