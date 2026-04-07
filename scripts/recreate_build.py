import argparse
import json
import logging
import os
from typing import Dict, List

from serval_auth_module import ServalBearerAuth
from serval_client_module import (
    Corpus,
    CorpusConfig,
    CorpusFileConfig,
    ParallelCorpusFilterConfig,
    PretranslateCorpusConfig,
    RemoteCaller,
    ResourceLink,
    TrainingCorpusConfig,
    TranslationBuild,
    TranslationBuildConfig,
    TranslationEngine,
    TranslationEngineConfig,
    TranslationParallelCorpus,
    TranslationParallelCorpusConfig,
)


def main():
    parser = argparse.ArgumentParser(
        description="Rerun a build job previously run on the 'source' Serval instance on the 'target' Serval instance."
    )
    parser.add_argument("--source-engine-id", help="Engine id", required=True)
    parser.add_argument(
        "--source-build-id",
        required=True,
        help="Build id",
    )
    parser.add_argument(
        "--source-client-id",
        required=True,
        help="Source Serval instance client id",
    )
    parser.add_argument(
        "--source-client-secret",
        required=True,
        help="Source Serval instance client secret",
    )
    parser.add_argument(
        "--source-client-auth-url",
        required=True,
        help="Source Serval instance client auth url",
    )
    parser.add_argument(
        "--source-client-host-url",
        required=True,
        help="Source Serval instance client host url",
    )
    parser.add_argument(
        "--target-client-id",
        default="",
        help="Serval client id (if none is provided env var SERVAL_CLIENT_ID will be used)",
    )
    parser.add_argument(
        "--target-client-secret",
        default="",
        help="Serval client secret (if none is provided env var SERVAL_CLIENT_SECRET will be used)",
    )
    parser.add_argument(
        "--target-client-auth-url",
        default="",
        help="Target Serval instance client auth url (if none is provided env var SERVAL_AUTH_URL will be used)",
    )
    parser.add_argument(
        "--target-client-host-url",
        default="",
        help="Target Serval instance client host url (if none is provided env var SERVAL_HOST_URL will be used)",
    )
    parser.add_argument(
        "--build-options",
        default="{}",
        help="Build options as a JSON string",
        type=json.loads,
    )
    args = parser.parse_args()

    logging.basicConfig(level=logging.INFO)
    logger = logging.getLogger("recreate_build")

    source_serval_auth = ServalBearerAuth(
        client_id=args.source_client_id,
        client_secret=args.source_client_secret,
        auth_url=args.source_client_auth_url,
    )
    source_client = RemoteCaller(
        url_prefix=args.source_client_host_url, auth=source_serval_auth
    )

    target_serval_auth = ServalBearerAuth(
        client_id=args.target_client_id,
        client_secret=args.target_client_secret,
        auth_url=args.target_client_auth_url,
    )
    target_client = RemoteCaller(
        url_prefix=(
            args.target_client_host_url
            if args.target_client_host_url
            else os.environ.get("SERVAL_HOST_URL")
        ),
        auth=target_serval_auth,
    )

    logger.info(
        f"Retrieving build information from source Serval instance ({args.source_client_host_url})..."
    )

    source_engine: TranslationEngine = source_client.translation_engines_get(
        args.source_engine_id
    )
    target_engine = json.loads(
        target_client.translation_engines_create(
            TranslationEngineConfig(
                type=source_engine.type,
                source_language=source_engine.source_language,
                target_language=source_engine.target_language,
                name=source_engine.name,
                is_model_persisted=source_engine.is_model_persisted,
            )
        )
    )

    source_parallel_corpora: List[
        TranslationParallelCorpus
    ] = source_client.translation_engines_get_all_parallel_corpora(source_engine.id)
    target_parallel_corpus_id_by_source_parallel_corpus_id: Dict[str, str] = {}
    target_corpus_id_by_source_corpus_id: Dict[str, str] = {}
    target_file_by_source_file_id: Dict[str, Dict] = {}

    def get_or_create_target_corpus(corpus_link: ResourceLink) -> str:
        corpus: Corpus = source_client.corpora_get(corpus_link.id)
        target_files = []
        for file_link in corpus.files:
            file = source_client.data_files_get(file_link.file.id)
            if file.id in target_file_by_source_file_id:
                target_file = target_file_by_source_file_id[file.id]
            else:
                file_contents = source_client.data_files_download(file.id)
                target_file = json.loads(
                    target_client.data_files_create(
                        file_contents, file.format, file.name
                    )
                )
                file_contents.close()
                target_file_by_source_file_id[file.id] = target_file
            target_files.append(target_file)
        if corpus.id not in target_corpus_id_by_source_corpus_id:
            target_corpus = json.loads(
                target_client.corpora_create(
                    CorpusConfig(
                        language=corpus.language,
                        files=[CorpusFileConfig(file_id=f["id"]) for f in target_files],
                        name=corpus.name,
                    )
                )
            )
            target_corpus_id_by_source_corpus_id[corpus.id] = target_corpus["id"]
            return target_corpus["id"]

        return target_corpus_id_by_source_corpus_id[corpus.id]

    for source_parallel_corpus in source_parallel_corpora:
        target_src_corpus_ids = []
        target_trg_corpus_ids = []
        for corpus_link in source_parallel_corpus.source_corpora:
            target_corpus_id = get_or_create_target_corpus(corpus_link)
            target_src_corpus_ids.append(target_corpus_id)
        for corpus_link in source_parallel_corpus.target_corpora:
            target_corpus_id = get_or_create_target_corpus(corpus_link)
            target_trg_corpus_ids.append(target_corpus_id)
        target_parallel_corpus = json.loads(
            target_client.translation_engines_add_parallel_corpus(
                target_engine["id"],
                TranslationParallelCorpusConfig(
                    source_corpus_ids=target_src_corpus_ids,
                    target_corpus_ids=target_trg_corpus_ids,
                ),
            )
        )
        target_parallel_corpus_id_by_source_parallel_corpus_id[
            source_parallel_corpus.id
        ] = target_parallel_corpus["id"]

    source_build: TranslationBuild = source_client.translation_engines_get_build(
        args.source_engine_id, args.source_build_id
    )
    target_translation_build_config = TranslationBuildConfig(
        name=source_build.name,
        options=args.build_options,
        train_on=[
            TrainingCorpusConfig(
                parallel_corpus_id=target_parallel_corpus_id_by_source_parallel_corpus_id[
                    config.parallel_corpus.id
                ],
                source_filters=[
                    ParallelCorpusFilterConfig(
                        corpus_id=target_corpus_id_by_source_corpus_id[
                            filter.corpus.id
                        ],
                        scripture_range=filter.scripture_range,
                        text_ids=filter.text_ids,
                    )
                    for filter in config.source_filters
                ],
                target_filters=[
                    ParallelCorpusFilterConfig(
                        corpus_id=target_corpus_id_by_source_corpus_id[
                            filter.corpus.id
                        ],
                        scripture_range=filter.scripture_range,
                        text_ids=filter.text_ids,
                    )
                    for filter in config.target_filters
                ],
            )
            for config in source_build.train_on
        ],
        pretranslate=[
            PretranslateCorpusConfig(
                parallel_corpus_id=target_parallel_corpus_id_by_source_parallel_corpus_id[
                    config.parallel_corpus.id
                ],
                source_filters=[
                    ParallelCorpusFilterConfig(
                        corpus_id=target_corpus_id_by_source_corpus_id[
                            filter.corpus.id
                        ],
                        scripture_range=filter.scripture_range,
                        text_ids=filter.text_ids,
                    )
                    for filter in config.source_filters
                ],
            )
            for config in source_build.pretranslate
        ],
    )

    target_build = json.loads(
        target_client.translation_engines_start_build(
            target_engine["id"], target_translation_build_config
        )
    )
    logger.info(
        f"Started build on target Serval instance ({args.target_client_host_url}) with build id {target_build['id']}"
    )


if __name__ == "__main__":
    main()
