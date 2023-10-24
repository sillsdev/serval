#!/usr/bin/env python3
# Automatically generated file by swagger_to. DO NOT EDIT OR APPEND ANYTHING!
"""Implements the client for Translation Engines."""

# pylint: skip-file
# pydocstyle: add-ignore=D105,D107,D401

import contextlib
import json
from typing import Any, BinaryIO, Dict, List, MutableMapping, Optional, cast

import requests
import requests.auth


def from_obj(obj: Any, expected: List[type], path: str = '') -> Any:
    """
    Checks and converts the given obj along the expected types.

    :param obj: to be converted
    :param expected: list of types representing the (nested) structure
    :param path: to the object used for debugging
    :return: the converted object
    """
    if not expected:
        raise ValueError("`expected` is empty, but at least one type needs to be specified.")

    exp = expected[0]

    if exp == float:
        if isinstance(obj, int):
            return float(obj)

        if isinstance(obj, float):
            return obj

        raise ValueError(
            'Expected object of type int or float at {!r}, but got {}.'.format(path, type(obj)))

    if exp in [bool, int, str, list, dict]:
        if not isinstance(obj, exp):
            raise ValueError(
                'Expected object of type {} at {!r}, but got {}.'.format(exp, path, type(obj)))

    if exp in [bool, int, float, str]:
        return obj

    if exp == list:
        lst = []  # type: List[Any]
        for i, value in enumerate(obj):
            lst.append(
                from_obj(value, expected=expected[1:], path='{}[{}]'.format(path, i)))

        return lst

    if exp == dict:
        adict = dict()  # type: Dict[str, Any]
        for key, value in obj.items():
            if not isinstance(key, str):
                raise ValueError(
                    'Expected a key of type str at path {!r}, got: {}'.format(path, type(key)))

            adict[key] = from_obj(value, expected=expected[1:], path='{}[{!r}]'.format(path, key))

        return adict

    if exp == DataFile:
        return data_file_from_obj(obj, path=path)

    if exp == TranslationEngine:
        return translation_engine_from_obj(obj, path=path)

    if exp == TranslationEngineConfig:
        return translation_engine_config_from_obj(obj, path=path)

    if exp == Queue:
        return queue_from_obj(obj, path=path)

    if exp == TranslationResult:
        return translation_result_from_obj(obj, path=path)

    if exp == AlignedWordPair:
        return aligned_word_pair_from_obj(obj, path=path)

    if exp == Phrase:
        return phrase_from_obj(obj, path=path)

    if exp == WordGraph:
        return word_graph_from_obj(obj, path=path)

    if exp == WordGraphArc:
        return word_graph_arc_from_obj(obj, path=path)

    if exp == SegmentPair:
        return segment_pair_from_obj(obj, path=path)

    if exp == TranslationCorpus:
        return translation_corpus_from_obj(obj, path=path)

    if exp == ResourceLink:
        return resource_link_from_obj(obj, path=path)

    if exp == TranslationCorpusFile:
        return translation_corpus_file_from_obj(obj, path=path)

    if exp == TranslationCorpusConfig:
        return translation_corpus_config_from_obj(obj, path=path)

    if exp == TranslationCorpusFileConfig:
        return translation_corpus_file_config_from_obj(obj, path=path)

    if exp == TranslationCorpusUpdateConfig:
        return translation_corpus_update_config_from_obj(obj, path=path)

    if exp == Pretranslation:
        return pretranslation_from_obj(obj, path=path)

    if exp == TranslationBuild:
        return translation_build_from_obj(obj, path=path)

    if exp == PretranslateCorpus:
        return pretranslate_corpus_from_obj(obj, path=path)

    if exp == TranslationBuildConfig:
        return translation_build_config_from_obj(obj, path=path)

    if exp == PretranslateCorpusConfig:
        return pretranslate_corpus_config_from_obj(obj, path=path)

    if exp == Webhook:
        return webhook_from_obj(obj, path=path)

    if exp == WebhookConfig:
        return webhook_config_from_obj(obj, path=path)

    raise ValueError("Unexpected `expected` type: {}".format(exp))


def to_jsonable(obj: Any, expected: List[type], path: str = "") -> Any:
    """
    Checks and converts the given object along the expected types to a JSON-able representation.

    :param obj: to be converted
    :param expected: list of types representing the (nested) structure
    :param path: path to the object used for debugging
    :return: JSON-able representation of the object
    """
    if not expected:
        raise ValueError("`expected` is empty, but at least one type needs to be specified.")

    exp = expected[0]
    if not isinstance(obj, exp):
        raise ValueError('Expected object of type {} at path {!r}, but got {}.'.format(
            exp, path, type(obj)))

    # Assert on primitive types to help type-hinting.
    if exp == bool:
        assert isinstance(obj, bool)
        return obj

    if exp == int:
        assert isinstance(obj, int)
        return obj

    if exp == float:
        assert isinstance(obj, float)
        return obj

    if exp == str:
        assert isinstance(obj, str)
        return obj

    if exp == list:
        assert isinstance(obj, list)

        lst = []  # type: List[Any]
        for i, value in enumerate(obj):
            lst.append(
                to_jsonable(value, expected=expected[1:], path='{}[{}]'.format(path, i)))

        return lst

    if exp == dict:
        assert isinstance(obj, dict)

        adict = dict()  # type: Dict[str, Any]
        for key, value in obj.items():
            if not isinstance(key, str):
                raise ValueError(
                    'Expected a key of type str at path {!r}, got: {}'.format(path, type(key)))

            adict[key] = to_jsonable(
                value,
                expected=expected[1:],
                path='{}[{!r}]'.format(path, key))

        return adict

    if exp == DataFile:
        assert isinstance(obj, DataFile)
        return data_file_to_jsonable(obj, path=path)

    if exp == TranslationEngine:
        assert isinstance(obj, TranslationEngine)
        return translation_engine_to_jsonable(obj, path=path)

    if exp == TranslationEngineConfig:
        assert isinstance(obj, TranslationEngineConfig)
        return translation_engine_config_to_jsonable(obj, path=path)

    if exp == Queue:
        assert isinstance(obj, Queue)
        return queue_to_jsonable(obj, path=path)

    if exp == TranslationResult:
        assert isinstance(obj, TranslationResult)
        return translation_result_to_jsonable(obj, path=path)

    if exp == AlignedWordPair:
        assert isinstance(obj, AlignedWordPair)
        return aligned_word_pair_to_jsonable(obj, path=path)

    if exp == Phrase:
        assert isinstance(obj, Phrase)
        return phrase_to_jsonable(obj, path=path)

    if exp == WordGraph:
        assert isinstance(obj, WordGraph)
        return word_graph_to_jsonable(obj, path=path)

    if exp == WordGraphArc:
        assert isinstance(obj, WordGraphArc)
        return word_graph_arc_to_jsonable(obj, path=path)

    if exp == SegmentPair:
        assert isinstance(obj, SegmentPair)
        return segment_pair_to_jsonable(obj, path=path)

    if exp == TranslationCorpus:
        assert isinstance(obj, TranslationCorpus)
        return translation_corpus_to_jsonable(obj, path=path)

    if exp == ResourceLink:
        assert isinstance(obj, ResourceLink)
        return resource_link_to_jsonable(obj, path=path)

    if exp == TranslationCorpusFile:
        assert isinstance(obj, TranslationCorpusFile)
        return translation_corpus_file_to_jsonable(obj, path=path)

    if exp == TranslationCorpusConfig:
        assert isinstance(obj, TranslationCorpusConfig)
        return translation_corpus_config_to_jsonable(obj, path=path)

    if exp == TranslationCorpusFileConfig:
        assert isinstance(obj, TranslationCorpusFileConfig)
        return translation_corpus_file_config_to_jsonable(obj, path=path)

    if exp == TranslationCorpusUpdateConfig:
        assert isinstance(obj, TranslationCorpusUpdateConfig)
        return translation_corpus_update_config_to_jsonable(obj, path=path)

    if exp == Pretranslation:
        assert isinstance(obj, Pretranslation)
        return pretranslation_to_jsonable(obj, path=path)

    if exp == TranslationBuild:
        assert isinstance(obj, TranslationBuild)
        return translation_build_to_jsonable(obj, path=path)

    if exp == PretranslateCorpus:
        assert isinstance(obj, PretranslateCorpus)
        return pretranslate_corpus_to_jsonable(obj, path=path)

    if exp == TranslationBuildConfig:
        assert isinstance(obj, TranslationBuildConfig)
        return translation_build_config_to_jsonable(obj, path=path)

    if exp == PretranslateCorpusConfig:
        assert isinstance(obj, PretranslateCorpusConfig)
        return pretranslate_corpus_config_to_jsonable(obj, path=path)

    if exp == Webhook:
        assert isinstance(obj, Webhook)
        return webhook_to_jsonable(obj, path=path)

    if exp == WebhookConfig:
        assert isinstance(obj, WebhookConfig)
        return webhook_config_to_jsonable(obj, path=path)

    raise ValueError("Unexpected `expected` type: {}".format(exp))


class DataFile:
    def __init__(
            self,
            id: str,
            url: str,
            format: str,
            revision: int,
            name: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

        self.format = format

        self.revision = revision

        self.name = name

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to data_file_to_jsonable.

        :return: JSON-able representation
        """
        return data_file_to_jsonable(self)


def new_data_file() -> DataFile:
    """Generates an instance of DataFile with default values."""
    return DataFile(
        id='',
        url='',
        format='',
        revision=0)


def data_file_from_obj(obj: Any, path: str = "") -> DataFile:
    """
    Generates an instance of DataFile from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of DataFile
    :param path: path to the object used for debugging
    :return: parsed instance of DataFile
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    format_from_obj = from_obj(
        obj['format'],
        expected=[str],
        path=path + '.format')  # type: str

    revision_from_obj = from_obj(
        obj['revision'],
        expected=[int],
        path=path + '.revision')  # type: int

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    return DataFile(
        id=id_from_obj,
        url=url_from_obj,
        format=format_from_obj,
        revision=revision_from_obj,
        name=name_from_obj)


def data_file_to_jsonable(
        data_file: DataFile,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of DataFile.

    :param data_file: instance of DataFile to be JSON-ized
    :param path: path to the data_file used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = data_file.id

    res['url'] = data_file.url

    res['format'] = data_file.format

    res['revision'] = data_file.revision

    if data_file.name is not None:
        res['name'] = data_file.name

    return res


class TranslationEngine:
    def __init__(
            self,
            id: str,
            url: str,
            source_language: str,
            target_language: str,
            type: str,
            is_building: bool,
            model_revision: int,
            confidence: float,
            corpus_size: int,
            name: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

        self.source_language = source_language

        self.target_language = target_language

        self.type = type

        self.is_building = is_building

        self.model_revision = model_revision

        self.confidence = confidence

        self.corpus_size = corpus_size

        self.name = name

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_engine_to_jsonable.

        :return: JSON-able representation
        """
        return translation_engine_to_jsonable(self)


def new_translation_engine() -> TranslationEngine:
    """Generates an instance of TranslationEngine with default values."""
    return TranslationEngine(
        id='',
        url='',
        source_language='',
        target_language='',
        type='',
        is_building=False,
        model_revision=0,
        confidence=0.0,
        corpus_size=0)


def translation_engine_from_obj(obj: Any, path: str = "") -> TranslationEngine:
    """
    Generates an instance of TranslationEngine from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationEngine
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationEngine
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    source_language_from_obj = from_obj(
        obj['sourceLanguage'],
        expected=[str],
        path=path + '.sourceLanguage')  # type: str

    target_language_from_obj = from_obj(
        obj['targetLanguage'],
        expected=[str],
        path=path + '.targetLanguage')  # type: str

    type_from_obj = from_obj(
        obj['type'],
        expected=[str],
        path=path + '.type')  # type: str

    is_building_from_obj = from_obj(
        obj['isBuilding'],
        expected=[bool],
        path=path + '.isBuilding')  # type: bool

    model_revision_from_obj = from_obj(
        obj['modelRevision'],
        expected=[int],
        path=path + '.modelRevision')  # type: int

    confidence_from_obj = from_obj(
        obj['confidence'],
        expected=[float],
        path=path + '.confidence')  # type: float

    corpus_size_from_obj = from_obj(
        obj['corpusSize'],
        expected=[int],
        path=path + '.corpusSize')  # type: int

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    return TranslationEngine(
        id=id_from_obj,
        url=url_from_obj,
        source_language=source_language_from_obj,
        target_language=target_language_from_obj,
        type=type_from_obj,
        is_building=is_building_from_obj,
        model_revision=model_revision_from_obj,
        confidence=confidence_from_obj,
        corpus_size=corpus_size_from_obj,
        name=name_from_obj)


def translation_engine_to_jsonable(
        translation_engine: TranslationEngine,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationEngine.

    :param translation_engine: instance of TranslationEngine to be JSON-ized
    :param path: path to the translation_engine used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = translation_engine.id

    res['url'] = translation_engine.url

    res['sourceLanguage'] = translation_engine.source_language

    res['targetLanguage'] = translation_engine.target_language

    res['type'] = translation_engine.type

    res['isBuilding'] = translation_engine.is_building

    res['modelRevision'] = translation_engine.model_revision

    res['confidence'] = translation_engine.confidence

    res['corpusSize'] = translation_engine.corpus_size

    if translation_engine.name is not None:
        res['name'] = translation_engine.name

    return res


class TranslationEngineConfig:
    def __init__(
            self,
            source_language: str,
            target_language: str,
            type: str,
            name: Optional[str] = None) -> None:
        """Initializes with the given values."""
        # The source language tag.
        self.source_language = source_language

        # The target language tag.
        self.target_language = target_language

        # The translation engine type.
        self.type = type

        # The translation engine name.
        self.name = name

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_engine_config_to_jsonable.

        :return: JSON-able representation
        """
        return translation_engine_config_to_jsonable(self)


def new_translation_engine_config() -> TranslationEngineConfig:
    """Generates an instance of TranslationEngineConfig with default values."""
    return TranslationEngineConfig(
        source_language='',
        target_language='',
        type='')


def translation_engine_config_from_obj(obj: Any, path: str = "") -> TranslationEngineConfig:
    """
    Generates an instance of TranslationEngineConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationEngineConfig
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationEngineConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_language_from_obj = from_obj(
        obj['sourceLanguage'],
        expected=[str],
        path=path + '.sourceLanguage')  # type: str

    target_language_from_obj = from_obj(
        obj['targetLanguage'],
        expected=[str],
        path=path + '.targetLanguage')  # type: str

    type_from_obj = from_obj(
        obj['type'],
        expected=[str],
        path=path + '.type')  # type: str

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    return TranslationEngineConfig(
        source_language=source_language_from_obj,
        target_language=target_language_from_obj,
        type=type_from_obj,
        name=name_from_obj)


def translation_engine_config_to_jsonable(
        translation_engine_config: TranslationEngineConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationEngineConfig.

    :param translation_engine_config: instance of TranslationEngineConfig to be JSON-ized
    :param path: path to the translation_engine_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceLanguage'] = translation_engine_config.source_language

    res['targetLanguage'] = translation_engine_config.target_language

    res['type'] = translation_engine_config.type

    if translation_engine_config.name is not None:
        res['name'] = translation_engine_config.name

    return res


class Queue:
    def __init__(
            self,
            size: int,
            engine_type: str) -> None:
        """Initializes with the given values."""
        self.size = size

        self.engine_type = engine_type

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to queue_to_jsonable.

        :return: JSON-able representation
        """
        return queue_to_jsonable(self)


def new_queue() -> Queue:
    """Generates an instance of Queue with default values."""
    return Queue(
        size=0,
        engine_type='')


def queue_from_obj(obj: Any, path: str = "") -> Queue:
    """
    Generates an instance of Queue from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of Queue
    :param path: path to the object used for debugging
    :return: parsed instance of Queue
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    size_from_obj = from_obj(
        obj['size'],
        expected=[int],
        path=path + '.size')  # type: int

    engine_type_from_obj = from_obj(
        obj['engineType'],
        expected=[str],
        path=path + '.engineType')  # type: str

    return Queue(
        size=size_from_obj,
        engine_type=engine_type_from_obj)


def queue_to_jsonable(
        queue: Queue,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of Queue.

    :param queue: instance of Queue to be JSON-ized
    :param path: path to the queue used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['size'] = queue.size

    res['engineType'] = queue.engine_type

    return res


class TranslationResult:
    def __init__(
            self,
            translation: str,
            source_tokens: List[str],
            target_tokens: List[str],
            confidences: List[float],
            sources: List[List[str]],
            alignment: List['AlignedWordPair'],
            phrases: List['Phrase']) -> None:
        """Initializes with the given values."""
        self.translation = translation

        self.source_tokens = source_tokens

        self.target_tokens = target_tokens

        self.confidences = confidences

        self.sources = sources

        self.alignment = alignment

        self.phrases = phrases

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_result_to_jsonable.

        :return: JSON-able representation
        """
        return translation_result_to_jsonable(self)


def new_translation_result() -> TranslationResult:
    """Generates an instance of TranslationResult with default values."""
    return TranslationResult(
        translation='',
        source_tokens=[],
        target_tokens=[],
        confidences=[],
        sources=[],
        alignment=[],
        phrases=[])


def translation_result_from_obj(obj: Any, path: str = "") -> TranslationResult:
    """
    Generates an instance of TranslationResult from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationResult
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationResult
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    translation_from_obj = from_obj(
        obj['translation'],
        expected=[str],
        path=path + '.translation')  # type: str

    source_tokens_from_obj = from_obj(
        obj['sourceTokens'],
        expected=[list, str],
        path=path + '.sourceTokens')  # type: List[str]

    target_tokens_from_obj = from_obj(
        obj['targetTokens'],
        expected=[list, str],
        path=path + '.targetTokens')  # type: List[str]

    confidences_from_obj = from_obj(
        obj['confidences'],
        expected=[list, float],
        path=path + '.confidences')  # type: List[float]

    sources_from_obj = from_obj(
        obj['sources'],
        expected=[list, list, str],
        path=path + '.sources')  # type: List[List[str]]

    alignment_from_obj = from_obj(
        obj['alignment'],
        expected=[list, AlignedWordPair],
        path=path + '.alignment')  # type: List['AlignedWordPair']

    phrases_from_obj = from_obj(
        obj['phrases'],
        expected=[list, Phrase],
        path=path + '.phrases')  # type: List['Phrase']

    return TranslationResult(
        translation=translation_from_obj,
        source_tokens=source_tokens_from_obj,
        target_tokens=target_tokens_from_obj,
        confidences=confidences_from_obj,
        sources=sources_from_obj,
        alignment=alignment_from_obj,
        phrases=phrases_from_obj)


def translation_result_to_jsonable(
        translation_result: TranslationResult,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationResult.

    :param translation_result: instance of TranslationResult to be JSON-ized
    :param path: path to the translation_result used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['translation'] = translation_result.translation

    res['sourceTokens'] = to_jsonable(
        translation_result.source_tokens,
        expected=[list, str],
        path='{}.sourceTokens'.format(path))

    res['targetTokens'] = to_jsonable(
        translation_result.target_tokens,
        expected=[list, str],
        path='{}.targetTokens'.format(path))

    res['confidences'] = to_jsonable(
        translation_result.confidences,
        expected=[list, float],
        path='{}.confidences'.format(path))

    res['sources'] = to_jsonable(
        translation_result.sources,
        expected=[list, list, str],
        path='{}.sources'.format(path))

    res['alignment'] = to_jsonable(
        translation_result.alignment,
        expected=[list, AlignedWordPair],
        path='{}.alignment'.format(path))

    res['phrases'] = to_jsonable(
        translation_result.phrases,
        expected=[list, Phrase],
        path='{}.phrases'.format(path))

    return res


class AlignedWordPair:
    def __init__(
            self,
            source_index: int,
            target_index: int) -> None:
        """Initializes with the given values."""
        self.source_index = source_index

        self.target_index = target_index

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to aligned_word_pair_to_jsonable.

        :return: JSON-able representation
        """
        return aligned_word_pair_to_jsonable(self)


def new_aligned_word_pair() -> AlignedWordPair:
    """Generates an instance of AlignedWordPair with default values."""
    return AlignedWordPair(
        source_index=0,
        target_index=0)


def aligned_word_pair_from_obj(obj: Any, path: str = "") -> AlignedWordPair:
    """
    Generates an instance of AlignedWordPair from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of AlignedWordPair
    :param path: path to the object used for debugging
    :return: parsed instance of AlignedWordPair
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_index_from_obj = from_obj(
        obj['sourceIndex'],
        expected=[int],
        path=path + '.sourceIndex')  # type: int

    target_index_from_obj = from_obj(
        obj['targetIndex'],
        expected=[int],
        path=path + '.targetIndex')  # type: int

    return AlignedWordPair(
        source_index=source_index_from_obj,
        target_index=target_index_from_obj)


def aligned_word_pair_to_jsonable(
        aligned_word_pair: AlignedWordPair,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of AlignedWordPair.

    :param aligned_word_pair: instance of AlignedWordPair to be JSON-ized
    :param path: path to the aligned_word_pair used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceIndex'] = aligned_word_pair.source_index

    res['targetIndex'] = aligned_word_pair.target_index

    return res


class Phrase:
    def __init__(
            self,
            source_segment_start: int,
            source_segment_end: int,
            target_segment_cut: int) -> None:
        """Initializes with the given values."""
        self.source_segment_start = source_segment_start

        self.source_segment_end = source_segment_end

        self.target_segment_cut = target_segment_cut

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to phrase_to_jsonable.

        :return: JSON-able representation
        """
        return phrase_to_jsonable(self)


def new_phrase() -> Phrase:
    """Generates an instance of Phrase with default values."""
    return Phrase(
        source_segment_start=0,
        source_segment_end=0,
        target_segment_cut=0)


def phrase_from_obj(obj: Any, path: str = "") -> Phrase:
    """
    Generates an instance of Phrase from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of Phrase
    :param path: path to the object used for debugging
    :return: parsed instance of Phrase
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_segment_start_from_obj = from_obj(
        obj['sourceSegmentStart'],
        expected=[int],
        path=path + '.sourceSegmentStart')  # type: int

    source_segment_end_from_obj = from_obj(
        obj['sourceSegmentEnd'],
        expected=[int],
        path=path + '.sourceSegmentEnd')  # type: int

    target_segment_cut_from_obj = from_obj(
        obj['targetSegmentCut'],
        expected=[int],
        path=path + '.targetSegmentCut')  # type: int

    return Phrase(
        source_segment_start=source_segment_start_from_obj,
        source_segment_end=source_segment_end_from_obj,
        target_segment_cut=target_segment_cut_from_obj)


def phrase_to_jsonable(
        phrase: Phrase,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of Phrase.

    :param phrase: instance of Phrase to be JSON-ized
    :param path: path to the phrase used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceSegmentStart'] = phrase.source_segment_start

    res['sourceSegmentEnd'] = phrase.source_segment_end

    res['targetSegmentCut'] = phrase.target_segment_cut

    return res


class WordGraph:
    def __init__(
            self,
            source_tokens: List[str],
            initial_state_score: float,
            final_states: List[int],
            arcs: List['WordGraphArc']) -> None:
        """Initializes with the given values."""
        self.source_tokens = source_tokens

        self.initial_state_score = initial_state_score

        self.final_states = final_states

        self.arcs = arcs

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to word_graph_to_jsonable.

        :return: JSON-able representation
        """
        return word_graph_to_jsonable(self)


def new_word_graph() -> WordGraph:
    """Generates an instance of WordGraph with default values."""
    return WordGraph(
        source_tokens=[],
        initial_state_score=0.0,
        final_states=[],
        arcs=[])


def word_graph_from_obj(obj: Any, path: str = "") -> WordGraph:
    """
    Generates an instance of WordGraph from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of WordGraph
    :param path: path to the object used for debugging
    :return: parsed instance of WordGraph
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_tokens_from_obj = from_obj(
        obj['sourceTokens'],
        expected=[list, str],
        path=path + '.sourceTokens')  # type: List[str]

    initial_state_score_from_obj = from_obj(
        obj['initialStateScore'],
        expected=[float],
        path=path + '.initialStateScore')  # type: float

    final_states_from_obj = from_obj(
        obj['finalStates'],
        expected=[list, int],
        path=path + '.finalStates')  # type: List[int]

    arcs_from_obj = from_obj(
        obj['arcs'],
        expected=[list, WordGraphArc],
        path=path + '.arcs')  # type: List['WordGraphArc']

    return WordGraph(
        source_tokens=source_tokens_from_obj,
        initial_state_score=initial_state_score_from_obj,
        final_states=final_states_from_obj,
        arcs=arcs_from_obj)


def word_graph_to_jsonable(
        word_graph: WordGraph,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of WordGraph.

    :param word_graph: instance of WordGraph to be JSON-ized
    :param path: path to the word_graph used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceTokens'] = to_jsonable(
        word_graph.source_tokens,
        expected=[list, str],
        path='{}.sourceTokens'.format(path))

    res['initialStateScore'] = word_graph.initial_state_score

    res['finalStates'] = to_jsonable(
        word_graph.final_states,
        expected=[list, int],
        path='{}.finalStates'.format(path))

    res['arcs'] = to_jsonable(
        word_graph.arcs,
        expected=[list, WordGraphArc],
        path='{}.arcs'.format(path))

    return res


class WordGraphArc:
    def __init__(
            self,
            prev_state: int,
            next_state: int,
            score: float,
            target_tokens: List[str],
            confidences: List[float],
            source_segment_start: int,
            source_segment_end: int,
            alignment: List['AlignedWordPair'],
            sources: List[List[str]]) -> None:
        """Initializes with the given values."""
        self.prev_state = prev_state

        self.next_state = next_state

        self.score = score

        self.target_tokens = target_tokens

        self.confidences = confidences

        self.source_segment_start = source_segment_start

        self.source_segment_end = source_segment_end

        self.alignment = alignment

        self.sources = sources

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to word_graph_arc_to_jsonable.

        :return: JSON-able representation
        """
        return word_graph_arc_to_jsonable(self)


def new_word_graph_arc() -> WordGraphArc:
    """Generates an instance of WordGraphArc with default values."""
    return WordGraphArc(
        prev_state=0,
        next_state=0,
        score=0.0,
        target_tokens=[],
        confidences=[],
        source_segment_start=0,
        source_segment_end=0,
        alignment=[],
        sources=[])


def word_graph_arc_from_obj(obj: Any, path: str = "") -> WordGraphArc:
    """
    Generates an instance of WordGraphArc from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of WordGraphArc
    :param path: path to the object used for debugging
    :return: parsed instance of WordGraphArc
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    prev_state_from_obj = from_obj(
        obj['prevState'],
        expected=[int],
        path=path + '.prevState')  # type: int

    next_state_from_obj = from_obj(
        obj['nextState'],
        expected=[int],
        path=path + '.nextState')  # type: int

    score_from_obj = from_obj(
        obj['score'],
        expected=[float],
        path=path + '.score')  # type: float

    target_tokens_from_obj = from_obj(
        obj['targetTokens'],
        expected=[list, str],
        path=path + '.targetTokens')  # type: List[str]

    confidences_from_obj = from_obj(
        obj['confidences'],
        expected=[list, float],
        path=path + '.confidences')  # type: List[float]

    source_segment_start_from_obj = from_obj(
        obj['sourceSegmentStart'],
        expected=[int],
        path=path + '.sourceSegmentStart')  # type: int

    source_segment_end_from_obj = from_obj(
        obj['sourceSegmentEnd'],
        expected=[int],
        path=path + '.sourceSegmentEnd')  # type: int

    alignment_from_obj = from_obj(
        obj['alignment'],
        expected=[list, AlignedWordPair],
        path=path + '.alignment')  # type: List['AlignedWordPair']

    sources_from_obj = from_obj(
        obj['sources'],
        expected=[list, list, str],
        path=path + '.sources')  # type: List[List[str]]

    return WordGraphArc(
        prev_state=prev_state_from_obj,
        next_state=next_state_from_obj,
        score=score_from_obj,
        target_tokens=target_tokens_from_obj,
        confidences=confidences_from_obj,
        source_segment_start=source_segment_start_from_obj,
        source_segment_end=source_segment_end_from_obj,
        alignment=alignment_from_obj,
        sources=sources_from_obj)


def word_graph_arc_to_jsonable(
        word_graph_arc: WordGraphArc,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of WordGraphArc.

    :param word_graph_arc: instance of WordGraphArc to be JSON-ized
    :param path: path to the word_graph_arc used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['prevState'] = word_graph_arc.prev_state

    res['nextState'] = word_graph_arc.next_state

    res['score'] = word_graph_arc.score

    res['targetTokens'] = to_jsonable(
        word_graph_arc.target_tokens,
        expected=[list, str],
        path='{}.targetTokens'.format(path))

    res['confidences'] = to_jsonable(
        word_graph_arc.confidences,
        expected=[list, float],
        path='{}.confidences'.format(path))

    res['sourceSegmentStart'] = word_graph_arc.source_segment_start

    res['sourceSegmentEnd'] = word_graph_arc.source_segment_end

    res['alignment'] = to_jsonable(
        word_graph_arc.alignment,
        expected=[list, AlignedWordPair],
        path='{}.alignment'.format(path))

    res['sources'] = to_jsonable(
        word_graph_arc.sources,
        expected=[list, list, str],
        path='{}.sources'.format(path))

    return res


class SegmentPair:
    def __init__(
            self,
            source_segment: str,
            target_segment: str,
            sentence_start: bool) -> None:
        """Initializes with the given values."""
        self.source_segment = source_segment

        self.target_segment = target_segment

        self.sentence_start = sentence_start

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to segment_pair_to_jsonable.

        :return: JSON-able representation
        """
        return segment_pair_to_jsonable(self)


def new_segment_pair() -> SegmentPair:
    """Generates an instance of SegmentPair with default values."""
    return SegmentPair(
        source_segment='',
        target_segment='',
        sentence_start=False)


def segment_pair_from_obj(obj: Any, path: str = "") -> SegmentPair:
    """
    Generates an instance of SegmentPair from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of SegmentPair
    :param path: path to the object used for debugging
    :return: parsed instance of SegmentPair
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_segment_from_obj = from_obj(
        obj['sourceSegment'],
        expected=[str],
        path=path + '.sourceSegment')  # type: str

    target_segment_from_obj = from_obj(
        obj['targetSegment'],
        expected=[str],
        path=path + '.targetSegment')  # type: str

    sentence_start_from_obj = from_obj(
        obj['sentenceStart'],
        expected=[bool],
        path=path + '.sentenceStart')  # type: bool

    return SegmentPair(
        source_segment=source_segment_from_obj,
        target_segment=target_segment_from_obj,
        sentence_start=sentence_start_from_obj)


def segment_pair_to_jsonable(
        segment_pair: SegmentPair,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of SegmentPair.

    :param segment_pair: instance of SegmentPair to be JSON-ized
    :param path: path to the segment_pair used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceSegment'] = segment_pair.source_segment

    res['targetSegment'] = segment_pair.target_segment

    res['sentenceStart'] = segment_pair.sentence_start

    return res


class TranslationCorpus:
    def __init__(
            self,
            id: str,
            url: str,
            engine: 'ResourceLink',
            source_language: str,
            target_language: str,
            source_files: List['TranslationCorpusFile'],
            target_files: List['TranslationCorpusFile'],
            name: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

        self.engine = engine

        self.source_language = source_language

        self.target_language = target_language

        self.source_files = source_files

        self.target_files = target_files

        self.name = name

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_corpus_to_jsonable.

        :return: JSON-able representation
        """
        return translation_corpus_to_jsonable(self)


def new_translation_corpus() -> TranslationCorpus:
    """Generates an instance of TranslationCorpus with default values."""
    return TranslationCorpus(
        id='',
        url='',
        engine=new_resource_link__,
        source_language='',
        target_language='',
        source_files=[],
        target_files=[])


def translation_corpus_from_obj(obj: Any, path: str = "") -> TranslationCorpus:
    """
    Generates an instance of TranslationCorpus from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationCorpus
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationCorpus
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    engine_from_obj = from_obj(
        obj['engine'],
        expected=[ResourceLink],
        path=path + '.engine')  # type: 'ResourceLink'

    source_language_from_obj = from_obj(
        obj['sourceLanguage'],
        expected=[str],
        path=path + '.sourceLanguage')  # type: str

    target_language_from_obj = from_obj(
        obj['targetLanguage'],
        expected=[str],
        path=path + '.targetLanguage')  # type: str

    source_files_from_obj = from_obj(
        obj['sourceFiles'],
        expected=[list, TranslationCorpusFile],
        path=path + '.sourceFiles')  # type: List['TranslationCorpusFile']

    target_files_from_obj = from_obj(
        obj['targetFiles'],
        expected=[list, TranslationCorpusFile],
        path=path + '.targetFiles')  # type: List['TranslationCorpusFile']

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    return TranslationCorpus(
        id=id_from_obj,
        url=url_from_obj,
        engine=engine_from_obj,
        source_language=source_language_from_obj,
        target_language=target_language_from_obj,
        source_files=source_files_from_obj,
        target_files=target_files_from_obj,
        name=name_from_obj)


def translation_corpus_to_jsonable(
        translation_corpus: TranslationCorpus,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationCorpus.

    :param translation_corpus: instance of TranslationCorpus to be JSON-ized
    :param path: path to the translation_corpus used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = translation_corpus.id

    res['url'] = translation_corpus.url

    res['engine'] = to_jsonable(
        translation_corpus.engine,
        expected=[ResourceLink],
        path='{}.engine'.format(path))

    res['sourceLanguage'] = translation_corpus.source_language

    res['targetLanguage'] = translation_corpus.target_language

    res['sourceFiles'] = to_jsonable(
        translation_corpus.source_files,
        expected=[list, TranslationCorpusFile],
        path='{}.sourceFiles'.format(path))

    res['targetFiles'] = to_jsonable(
        translation_corpus.target_files,
        expected=[list, TranslationCorpusFile],
        path='{}.targetFiles'.format(path))

    if translation_corpus.name is not None:
        res['name'] = translation_corpus.name

    return res


class ResourceLink:
    def __init__(
            self,
            id: str,
            url: str) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to resource_link_to_jsonable.

        :return: JSON-able representation
        """
        return resource_link_to_jsonable(self)


def new_resource_link() -> ResourceLink:
    """Generates an instance of ResourceLink with default values."""
    return ResourceLink(
        id='',
        url='')


def resource_link_from_obj(obj: Any, path: str = "") -> ResourceLink:
    """
    Generates an instance of ResourceLink from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of ResourceLink
    :param path: path to the object used for debugging
    :return: parsed instance of ResourceLink
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    return ResourceLink(
        id=id_from_obj,
        url=url_from_obj)


def resource_link_to_jsonable(
        resource_link: ResourceLink,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of ResourceLink.

    :param resource_link: instance of ResourceLink to be JSON-ized
    :param path: path to the resource_link used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = resource_link.id

    res['url'] = resource_link.url

    return res


class TranslationCorpusFile:
    def __init__(
            self,
            file: 'ResourceLink',
            text_id: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.file = file

        self.text_id = text_id

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_corpus_file_to_jsonable.

        :return: JSON-able representation
        """
        return translation_corpus_file_to_jsonable(self)


def new_translation_corpus_file() -> TranslationCorpusFile:
    """Generates an instance of TranslationCorpusFile with default values."""
    return TranslationCorpusFile(
        file=new_resource_link__)


def translation_corpus_file_from_obj(obj: Any, path: str = "") -> TranslationCorpusFile:
    """
    Generates an instance of TranslationCorpusFile from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationCorpusFile
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationCorpusFile
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    file_from_obj = from_obj(
        obj['file'],
        expected=[ResourceLink],
        path=path + '.file')  # type: 'ResourceLink'

    obj_text_id = obj.get('textId', None)
    if obj_text_id is not None:
        text_id_from_obj = from_obj(
            obj_text_id,
            expected=[str],
            path=path + '.textId')  # type: Optional[str]
    else:
        text_id_from_obj = None

    return TranslationCorpusFile(
        file=file_from_obj,
        text_id=text_id_from_obj)


def translation_corpus_file_to_jsonable(
        translation_corpus_file: TranslationCorpusFile,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationCorpusFile.

    :param translation_corpus_file: instance of TranslationCorpusFile to be JSON-ized
    :param path: path to the translation_corpus_file used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['file'] = to_jsonable(
        translation_corpus_file.file,
        expected=[ResourceLink],
        path='{}.file'.format(path))

    if translation_corpus_file.text_id is not None:
        res['textId'] = translation_corpus_file.text_id

    return res


class TranslationCorpusConfig:
    def __init__(
            self,
            source_language: str,
            target_language: str,
            source_files: List['TranslationCorpusFileConfig'],
            target_files: List['TranslationCorpusFileConfig'],
            name: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.source_language = source_language

        self.target_language = target_language

        self.source_files = source_files

        self.target_files = target_files

        # The corpus name.
        self.name = name

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_corpus_config_to_jsonable.

        :return: JSON-able representation
        """
        return translation_corpus_config_to_jsonable(self)


def new_translation_corpus_config() -> TranslationCorpusConfig:
    """Generates an instance of TranslationCorpusConfig with default values."""
    return TranslationCorpusConfig(
        source_language='',
        target_language='',
        source_files=[],
        target_files=[])


def translation_corpus_config_from_obj(obj: Any, path: str = "") -> TranslationCorpusConfig:
    """
    Generates an instance of TranslationCorpusConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationCorpusConfig
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationCorpusConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    source_language_from_obj = from_obj(
        obj['sourceLanguage'],
        expected=[str],
        path=path + '.sourceLanguage')  # type: str

    target_language_from_obj = from_obj(
        obj['targetLanguage'],
        expected=[str],
        path=path + '.targetLanguage')  # type: str

    source_files_from_obj = from_obj(
        obj['sourceFiles'],
        expected=[list, TranslationCorpusFileConfig],
        path=path + '.sourceFiles')  # type: List['TranslationCorpusFileConfig']

    target_files_from_obj = from_obj(
        obj['targetFiles'],
        expected=[list, TranslationCorpusFileConfig],
        path=path + '.targetFiles')  # type: List['TranslationCorpusFileConfig']

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    return TranslationCorpusConfig(
        source_language=source_language_from_obj,
        target_language=target_language_from_obj,
        source_files=source_files_from_obj,
        target_files=target_files_from_obj,
        name=name_from_obj)


def translation_corpus_config_to_jsonable(
        translation_corpus_config: TranslationCorpusConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationCorpusConfig.

    :param translation_corpus_config: instance of TranslationCorpusConfig to be JSON-ized
    :param path: path to the translation_corpus_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['sourceLanguage'] = translation_corpus_config.source_language

    res['targetLanguage'] = translation_corpus_config.target_language

    res['sourceFiles'] = to_jsonable(
        translation_corpus_config.source_files,
        expected=[list, TranslationCorpusFileConfig],
        path='{}.sourceFiles'.format(path))

    res['targetFiles'] = to_jsonable(
        translation_corpus_config.target_files,
        expected=[list, TranslationCorpusFileConfig],
        path='{}.targetFiles'.format(path))

    if translation_corpus_config.name is not None:
        res['name'] = translation_corpus_config.name

    return res


class TranslationCorpusFileConfig:
    def __init__(
            self,
            file_id: str,
            text_id: Optional[str] = None) -> None:
        """Initializes with the given values."""
        self.file_id = file_id

        self.text_id = text_id

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_corpus_file_config_to_jsonable.

        :return: JSON-able representation
        """
        return translation_corpus_file_config_to_jsonable(self)


def new_translation_corpus_file_config() -> TranslationCorpusFileConfig:
    """Generates an instance of TranslationCorpusFileConfig with default values."""
    return TranslationCorpusFileConfig(
        file_id='')


def translation_corpus_file_config_from_obj(obj: Any, path: str = "") -> TranslationCorpusFileConfig:
    """
    Generates an instance of TranslationCorpusFileConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationCorpusFileConfig
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationCorpusFileConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    file_id_from_obj = from_obj(
        obj['fileId'],
        expected=[str],
        path=path + '.fileId')  # type: str

    obj_text_id = obj.get('textId', None)
    if obj_text_id is not None:
        text_id_from_obj = from_obj(
            obj_text_id,
            expected=[str],
            path=path + '.textId')  # type: Optional[str]
    else:
        text_id_from_obj = None

    return TranslationCorpusFileConfig(
        file_id=file_id_from_obj,
        text_id=text_id_from_obj)


def translation_corpus_file_config_to_jsonable(
        translation_corpus_file_config: TranslationCorpusFileConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationCorpusFileConfig.

    :param translation_corpus_file_config: instance of TranslationCorpusFileConfig to be JSON-ized
    :param path: path to the translation_corpus_file_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['fileId'] = translation_corpus_file_config.file_id

    if translation_corpus_file_config.text_id is not None:
        res['textId'] = translation_corpus_file_config.text_id

    return res


class TranslationCorpusUpdateConfig:
    def __init__(
            self,
            source_files: Optional[List['TranslationCorpusFileConfig']] = None,
            target_files: Optional[List['TranslationCorpusFileConfig']] = None) -> None:
        """Initializes with the given values."""
        self.source_files = source_files

        self.target_files = target_files

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_corpus_update_config_to_jsonable.

        :return: JSON-able representation
        """
        return translation_corpus_update_config_to_jsonable(self)


def new_translation_corpus_update_config() -> TranslationCorpusUpdateConfig:
    """Generates an instance of TranslationCorpusUpdateConfig with default values."""
    return TranslationCorpusUpdateConfig()


def translation_corpus_update_config_from_obj(obj: Any, path: str = "") -> TranslationCorpusUpdateConfig:
    """
    Generates an instance of TranslationCorpusUpdateConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationCorpusUpdateConfig
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationCorpusUpdateConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    obj_source_files = obj.get('sourceFiles', None)
    if obj_source_files is not None:
        source_files_from_obj = from_obj(
            obj_source_files,
            expected=[list, TranslationCorpusFileConfig],
            path=path + '.sourceFiles')  # type: Optional[List['TranslationCorpusFileConfig']]
    else:
        source_files_from_obj = None

    obj_target_files = obj.get('targetFiles', None)
    if obj_target_files is not None:
        target_files_from_obj = from_obj(
            obj_target_files,
            expected=[list, TranslationCorpusFileConfig],
            path=path + '.targetFiles')  # type: Optional[List['TranslationCorpusFileConfig']]
    else:
        target_files_from_obj = None

    return TranslationCorpusUpdateConfig(
        source_files=source_files_from_obj,
        target_files=target_files_from_obj)


def translation_corpus_update_config_to_jsonable(
        translation_corpus_update_config: TranslationCorpusUpdateConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationCorpusUpdateConfig.

    :param translation_corpus_update_config: instance of TranslationCorpusUpdateConfig to be JSON-ized
    :param path: path to the translation_corpus_update_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    if translation_corpus_update_config.source_files is not None:
        res['sourceFiles'] = to_jsonable(
        translation_corpus_update_config.source_files,
        expected=[list, TranslationCorpusFileConfig],
        path='{}.sourceFiles'.format(path))

    if translation_corpus_update_config.target_files is not None:
        res['targetFiles'] = to_jsonable(
        translation_corpus_update_config.target_files,
        expected=[list, TranslationCorpusFileConfig],
        path='{}.targetFiles'.format(path))

    return res


class Pretranslation:
    def __init__(
            self,
            text_id: str,
            refs: List[str],
            translation: str) -> None:
        """Initializes with the given values."""
        self.text_id = text_id

        self.refs = refs

        self.translation = translation

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to pretranslation_to_jsonable.

        :return: JSON-able representation
        """
        return pretranslation_to_jsonable(self)


def new_pretranslation() -> Pretranslation:
    """Generates an instance of Pretranslation with default values."""
    return Pretranslation(
        text_id='',
        refs=[],
        translation='')


def pretranslation_from_obj(obj: Any, path: str = "") -> Pretranslation:
    """
    Generates an instance of Pretranslation from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of Pretranslation
    :param path: path to the object used for debugging
    :return: parsed instance of Pretranslation
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    text_id_from_obj = from_obj(
        obj['textId'],
        expected=[str],
        path=path + '.textId')  # type: str

    refs_from_obj = from_obj(
        obj['refs'],
        expected=[list, str],
        path=path + '.refs')  # type: List[str]

    translation_from_obj = from_obj(
        obj['translation'],
        expected=[str],
        path=path + '.translation')  # type: str

    return Pretranslation(
        text_id=text_id_from_obj,
        refs=refs_from_obj,
        translation=translation_from_obj)


def pretranslation_to_jsonable(
        pretranslation: Pretranslation,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of Pretranslation.

    :param pretranslation: instance of Pretranslation to be JSON-ized
    :param path: path to the pretranslation used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['textId'] = pretranslation.text_id

    res['refs'] = to_jsonable(
        pretranslation.refs,
        expected=[list, str],
        path='{}.refs'.format(path))

    res['translation'] = pretranslation.translation

    return res


class TranslationBuild:
    def __init__(
            self,
            id: str,
            url: str,
            revision: int,
            engine: 'ResourceLink',
            step: int,
            state: str,
            name: Optional[str] = None,
            pretranslate: Optional[List['PretranslateCorpus']] = None,
            percent_completed: Optional[float] = None,
            message: Optional[str] = None,
            queue_depth: Optional[int] = None,
            date_finished: Optional[str] = None,
            options: Optional[Any] = None) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

        self.revision = revision

        self.engine = engine

        self.step = step

        # The current build job state.
        self.state = state

        self.name = name

        self.pretranslate = pretranslate

        self.percent_completed = percent_completed

        self.message = message

        self.queue_depth = queue_depth

        self.date_finished = date_finished

        self.options = options

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_build_to_jsonable.

        :return: JSON-able representation
        """
        return translation_build_to_jsonable(self)


def new_translation_build() -> TranslationBuild:
    """Generates an instance of TranslationBuild with default values."""
    return TranslationBuild(
        id='',
        url='',
        revision=0,
        engine=new_resource_link__,
        step=0,
        state='')


def translation_build_from_obj(obj: Any, path: str = "") -> TranslationBuild:
    """
    Generates an instance of TranslationBuild from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationBuild
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationBuild
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    revision_from_obj = from_obj(
        obj['revision'],
        expected=[int],
        path=path + '.revision')  # type: int

    engine_from_obj = from_obj(
        obj['engine'],
        expected=[ResourceLink],
        path=path + '.engine')  # type: 'ResourceLink'

    step_from_obj = from_obj(
        obj['step'],
        expected=[int],
        path=path + '.step')  # type: int

    state_from_obj = from_obj(
        obj['state'],
        expected=[str],
        path=path + '.state')  # type: str

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    obj_pretranslate = obj.get('pretranslate', None)
    if obj_pretranslate is not None:
        pretranslate_from_obj = from_obj(
            obj_pretranslate,
            expected=[list, PretranslateCorpus],
            path=path + '.pretranslate')  # type: Optional[List['PretranslateCorpus']]
    else:
        pretranslate_from_obj = None

    obj_percent_completed = obj.get('percentCompleted', None)
    if obj_percent_completed is not None:
        percent_completed_from_obj = from_obj(
            obj_percent_completed,
            expected=[float],
            path=path + '.percentCompleted')  # type: Optional[float]
    else:
        percent_completed_from_obj = None

    obj_message = obj.get('message', None)
    if obj_message is not None:
        message_from_obj = from_obj(
            obj_message,
            expected=[str],
            path=path + '.message')  # type: Optional[str]
    else:
        message_from_obj = None

    obj_queue_depth = obj.get('queueDepth', None)
    if obj_queue_depth is not None:
        queue_depth_from_obj = from_obj(
            obj_queue_depth,
            expected=[int],
            path=path + '.queueDepth')  # type: Optional[int]
    else:
        queue_depth_from_obj = None

    obj_date_finished = obj.get('dateFinished', None)
    if obj_date_finished is not None:
        date_finished_from_obj = from_obj(
            obj_date_finished,
            expected=[str],
            path=path + '.dateFinished')  # type: Optional[str]
    else:
        date_finished_from_obj = None

    options_from_obj = obj.get('options', None)

    return TranslationBuild(
        id=id_from_obj,
        url=url_from_obj,
        revision=revision_from_obj,
        engine=engine_from_obj,
        step=step_from_obj,
        state=state_from_obj,
        name=name_from_obj,
        pretranslate=pretranslate_from_obj,
        percent_completed=percent_completed_from_obj,
        message=message_from_obj,
        queue_depth=queue_depth_from_obj,
        date_finished=date_finished_from_obj,
        options=options_from_obj)


def translation_build_to_jsonable(
        translation_build: TranslationBuild,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationBuild.

    :param translation_build: instance of TranslationBuild to be JSON-ized
    :param path: path to the translation_build used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = translation_build.id

    res['url'] = translation_build.url

    res['revision'] = translation_build.revision

    res['engine'] = to_jsonable(
        translation_build.engine,
        expected=[ResourceLink],
        path='{}.engine'.format(path))

    res['step'] = translation_build.step

    res['state'] = translation_build.state

    if translation_build.name is not None:
        res['name'] = translation_build.name

    if translation_build.pretranslate is not None:
        res['pretranslate'] = to_jsonable(
        translation_build.pretranslate,
        expected=[list, PretranslateCorpus],
        path='{}.pretranslate'.format(path))

    if translation_build.percent_completed is not None:
        res['percentCompleted'] = translation_build.percent_completed

    if translation_build.message is not None:
        res['message'] = translation_build.message

    if translation_build.queue_depth is not None:
        res['queueDepth'] = translation_build.queue_depth

    if translation_build.date_finished is not None:
        res['dateFinished'] = translation_build.date_finished

    if translation_build.options is not None:
        res['options'] = translation_build.options

    return res


class PretranslateCorpus:
    def __init__(
            self,
            corpus: 'ResourceLink',
            text_ids: Optional[List[str]] = None) -> None:
        """Initializes with the given values."""
        self.corpus = corpus

        self.text_ids = text_ids

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to pretranslate_corpus_to_jsonable.

        :return: JSON-able representation
        """
        return pretranslate_corpus_to_jsonable(self)


def new_pretranslate_corpus() -> PretranslateCorpus:
    """Generates an instance of PretranslateCorpus with default values."""
    return PretranslateCorpus(
        corpus=new_resource_link__)


def pretranslate_corpus_from_obj(obj: Any, path: str = "") -> PretranslateCorpus:
    """
    Generates an instance of PretranslateCorpus from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of PretranslateCorpus
    :param path: path to the object used for debugging
    :return: parsed instance of PretranslateCorpus
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    corpus_from_obj = from_obj(
        obj['corpus'],
        expected=[ResourceLink],
        path=path + '.corpus')  # type: 'ResourceLink'

    obj_text_ids = obj.get('textIds', None)
    if obj_text_ids is not None:
        text_ids_from_obj = from_obj(
            obj_text_ids,
            expected=[list, str],
            path=path + '.textIds')  # type: Optional[List[str]]
    else:
        text_ids_from_obj = None

    return PretranslateCorpus(
        corpus=corpus_from_obj,
        text_ids=text_ids_from_obj)


def pretranslate_corpus_to_jsonable(
        pretranslate_corpus: PretranslateCorpus,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of PretranslateCorpus.

    :param pretranslate_corpus: instance of PretranslateCorpus to be JSON-ized
    :param path: path to the pretranslate_corpus used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['corpus'] = to_jsonable(
        pretranslate_corpus.corpus,
        expected=[ResourceLink],
        path='{}.corpus'.format(path))

    if pretranslate_corpus.text_ids is not None:
        res['textIds'] = to_jsonable(
        pretranslate_corpus.text_ids,
        expected=[list, str],
        path='{}.textIds'.format(path))

    return res


class TranslationBuildConfig:
    def __init__(
            self,
            name: Optional[str] = None,
            pretranslate: Optional[List['PretranslateCorpusConfig']] = None,
            options: Optional[Any] = None) -> None:
        """Initializes with the given values."""
        self.name = name

        self.pretranslate = pretranslate

        self.options = options

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to translation_build_config_to_jsonable.

        :return: JSON-able representation
        """
        return translation_build_config_to_jsonable(self)


def new_translation_build_config() -> TranslationBuildConfig:
    """Generates an instance of TranslationBuildConfig with default values."""
    return TranslationBuildConfig()


def translation_build_config_from_obj(obj: Any, path: str = "") -> TranslationBuildConfig:
    """
    Generates an instance of TranslationBuildConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of TranslationBuildConfig
    :param path: path to the object used for debugging
    :return: parsed instance of TranslationBuildConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    obj_name = obj.get('name', None)
    if obj_name is not None:
        name_from_obj = from_obj(
            obj_name,
            expected=[str],
            path=path + '.name')  # type: Optional[str]
    else:
        name_from_obj = None

    obj_pretranslate = obj.get('pretranslate', None)
    if obj_pretranslate is not None:
        pretranslate_from_obj = from_obj(
            obj_pretranslate,
            expected=[list, PretranslateCorpusConfig],
            path=path + '.pretranslate')  # type: Optional[List['PretranslateCorpusConfig']]
    else:
        pretranslate_from_obj = None

    options_from_obj = obj.get('options', None)

    return TranslationBuildConfig(
        name=name_from_obj,
        pretranslate=pretranslate_from_obj,
        options=options_from_obj)


def translation_build_config_to_jsonable(
        translation_build_config: TranslationBuildConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of TranslationBuildConfig.

    :param translation_build_config: instance of TranslationBuildConfig to be JSON-ized
    :param path: path to the translation_build_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    if translation_build_config.name is not None:
        res['name'] = translation_build_config.name

    if translation_build_config.pretranslate is not None:
        res['pretranslate'] = to_jsonable(
        translation_build_config.pretranslate,
        expected=[list, PretranslateCorpusConfig],
        path='{}.pretranslate'.format(path))

    if translation_build_config.options is not None:
        res['options'] = translation_build_config.options

    return res


class PretranslateCorpusConfig:
    def __init__(
            self,
            corpus_id: str,
            text_ids: Optional[List[str]] = None) -> None:
        """Initializes with the given values."""
        self.corpus_id = corpus_id

        self.text_ids = text_ids

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to pretranslate_corpus_config_to_jsonable.

        :return: JSON-able representation
        """
        return pretranslate_corpus_config_to_jsonable(self)


def new_pretranslate_corpus_config() -> PretranslateCorpusConfig:
    """Generates an instance of PretranslateCorpusConfig with default values."""
    return PretranslateCorpusConfig(
        corpus_id='')


def pretranslate_corpus_config_from_obj(obj: Any, path: str = "") -> PretranslateCorpusConfig:
    """
    Generates an instance of PretranslateCorpusConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of PretranslateCorpusConfig
    :param path: path to the object used for debugging
    :return: parsed instance of PretranslateCorpusConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    corpus_id_from_obj = from_obj(
        obj['corpusId'],
        expected=[str],
        path=path + '.corpusId')  # type: str

    obj_text_ids = obj.get('textIds', None)
    if obj_text_ids is not None:
        text_ids_from_obj = from_obj(
            obj_text_ids,
            expected=[list, str],
            path=path + '.textIds')  # type: Optional[List[str]]
    else:
        text_ids_from_obj = None

    return PretranslateCorpusConfig(
        corpus_id=corpus_id_from_obj,
        text_ids=text_ids_from_obj)


def pretranslate_corpus_config_to_jsonable(
        pretranslate_corpus_config: PretranslateCorpusConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of PretranslateCorpusConfig.

    :param pretranslate_corpus_config: instance of PretranslateCorpusConfig to be JSON-ized
    :param path: path to the pretranslate_corpus_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['corpusId'] = pretranslate_corpus_config.corpus_id

    if pretranslate_corpus_config.text_ids is not None:
        res['textIds'] = to_jsonable(
        pretranslate_corpus_config.text_ids,
        expected=[list, str],
        path='{}.textIds'.format(path))

    return res


class Webhook:
    def __init__(
            self,
            id: str,
            url: str,
            payload_url: str,
            events: List[str]) -> None:
        """Initializes with the given values."""
        self.id = id

        self.url = url

        self.payload_url = payload_url

        self.events = events

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to webhook_to_jsonable.

        :return: JSON-able representation
        """
        return webhook_to_jsonable(self)


def new_webhook() -> Webhook:
    """Generates an instance of Webhook with default values."""
    return Webhook(
        id='',
        url='',
        payload_url='',
        events=[])


def webhook_from_obj(obj: Any, path: str = "") -> Webhook:
    """
    Generates an instance of Webhook from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of Webhook
    :param path: path to the object used for debugging
    :return: parsed instance of Webhook
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    id_from_obj = from_obj(
        obj['id'],
        expected=[str],
        path=path + '.id')  # type: str

    url_from_obj = from_obj(
        obj['url'],
        expected=[str],
        path=path + '.url')  # type: str

    payload_url_from_obj = from_obj(
        obj['payloadUrl'],
        expected=[str],
        path=path + '.payloadUrl')  # type: str

    events_from_obj = from_obj(
        obj['events'],
        expected=[list, str],
        path=path + '.events')  # type: List[str]

    return Webhook(
        id=id_from_obj,
        url=url_from_obj,
        payload_url=payload_url_from_obj,
        events=events_from_obj)


def webhook_to_jsonable(
        webhook: Webhook,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of Webhook.

    :param webhook: instance of Webhook to be JSON-ized
    :param path: path to the webhook used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['id'] = webhook.id

    res['url'] = webhook.url

    res['payloadUrl'] = webhook.payload_url

    res['events'] = to_jsonable(
        webhook.events,
        expected=[list, str],
        path='{}.events'.format(path))

    return res


class WebhookConfig:
    def __init__(
            self,
            payload_url: str,
            secret: str,
            events: List[str]) -> None:
        """Initializes with the given values."""
        # The payload URL.
        self.payload_url = payload_url

        # The shared secret.
        self.secret = secret

        # The webhook events.
        self.events = events

    def to_jsonable(self) -> MutableMapping[str, Any]:
        """
        Dispatches the conversion to webhook_config_to_jsonable.

        :return: JSON-able representation
        """
        return webhook_config_to_jsonable(self)


def new_webhook_config() -> WebhookConfig:
    """Generates an instance of WebhookConfig with default values."""
    return WebhookConfig(
        payload_url='',
        secret='',
        events=[])


def webhook_config_from_obj(obj: Any, path: str = "") -> WebhookConfig:
    """
    Generates an instance of WebhookConfig from a dictionary object.

    :param obj: a JSON-ed dictionary object representing an instance of WebhookConfig
    :param path: path to the object used for debugging
    :return: parsed instance of WebhookConfig
    """
    if not isinstance(obj, dict):
        raise ValueError('Expected a dict at path {}, but got: {}'.format(path, type(obj)))

    for key in obj:
        if not isinstance(key, str):
            raise ValueError(
                'Expected a key of type str at path {}, but got: {}'.format(path, type(key)))

    payload_url_from_obj = from_obj(
        obj['payloadUrl'],
        expected=[str],
        path=path + '.payloadUrl')  # type: str

    secret_from_obj = from_obj(
        obj['secret'],
        expected=[str],
        path=path + '.secret')  # type: str

    events_from_obj = from_obj(
        obj['events'],
        expected=[list, str],
        path=path + '.events')  # type: List[str]

    return WebhookConfig(
        payload_url=payload_url_from_obj,
        secret=secret_from_obj,
        events=events_from_obj)


def webhook_config_to_jsonable(
        webhook_config: WebhookConfig,
        path: str = "") -> MutableMapping[str, Any]:
    """
    Generates a JSON-able mapping from an instance of WebhookConfig.

    :param webhook_config: instance of WebhookConfig to be JSON-ized
    :param path: path to the webhook_config used for debugging
    :return: a JSON-able representation
    """
    res = dict()  # type: Dict[str, Any]

    res['payloadUrl'] = webhook_config.payload_url

    res['secret'] = webhook_config.secret

    res['events'] = to_jsonable(
        webhook_config.events,
        expected=[list, str],
        path='{}.events'.format(path))

    return res


class RemoteCaller:
    """Executes the remote calls to the server."""

    def __init__(
        self,
        url_prefix: str,
        auth: Optional[requests.auth.AuthBase] = None,
        session: Optional[requests.Session] = None) -> None:
        self.url_prefix = url_prefix
        self.auth = auth
        self.session = session

        if not self.session:
            self.session = requests.Session()
            self.session.auth = self.auth

    def data_files_get_all(self) -> List['DataFile']:
        """
        Send a get request to /api/v1/files.

        :return: A list of all files owned by the client
        """
        url = self.url_prefix + '/api/v1/files'

        resp = self.session.request(method='get', url=url)

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, DataFile])

    def data_files_create(
            self,
            file: BinaryIO,
            format: str,
            name: Optional[str] = None) -> bytes:
        """
        Sample request:
                    
            POST /files
            {
               "format": "text",
               "name": "myTeam:myProject:myFile.txt"
            }

        :param file: The file to upload.  Max size: 100MB
        :param format:
            File format options:
            * **Text**: One translation unit (a.k.a., verse) per line
              * If a line contains a tab, characters before the tab are used as a unique identifier for the line, characters after the tab are understood as the content of the verse, and if there is another tab following the verse content, characters after this second tab are assumed to be column codes like "ss" etc. for sectioning and other formatting. See this example of a tab-delimited text file:
                > verse_001_005 (tab) Ὑπομνῆσαι δὲ ὑμᾶς βούλομαι , εἰδότας ὑμᾶς ἅπαξ τοῦτο
                > verse_001_006 (tab) Ἀγγέλους τε τοὺς μὴ τηρήσαντας τὴν ἑαυτῶν ἀρχήν , ἀλλὰ (tab) ss
                > verse_001_007 (tab) Ὡς Σόδομα καὶ Γόμορρα , καὶ αἱ περὶ αὐτὰς πόλεις (tab) ss
              * Otherwise, *no tabs* should be used in the file and a unique identifier will generated for each translation unit based on the line number.
            * **Paratext**: A complete, zipped Paratext project backup: that is, a .zip archive of files including the USFM files and "Settings.xml" file. To generate a zipped backup for a project in Paratext, navigate to "Paratext/Advanced/Backup project to file..." and follow the dialogue.
        :param name:
            A name to help identify and distinguish the file.
            Recommendation: Create a multi-part name to distinguish between projects, uses, languages, etc.
            The name does not have to be unique.
            Example: myTranslationTeam:myProject:myLanguage:myFile.txt

        :return:
        """
        url = self.url_prefix + '/api/v1/files'

        data = {}  # type: Dict[str, str]

        data['format'] = format

        if name is not None:
            data['name'] = name

        files = {}  # type: Dict[str, BinaryIO]

        files['file'] = file

        resp = self.session.request(
            method='post',
            url=url,
            data=data,
            files=files,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def data_files_get(
            self,
            id: str) -> 'DataFile':
        """
        Send a get request to /api/v1/files/{id}.

        :param id: The unique identifier for the file

        :return: The file exists
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/files/',
            str(id)])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[DataFile])

    def data_files_update(
            self,
            id: str,
            file: BinaryIO) -> 'DataFile':
        """
        Send a patch request to /api/v1/files/{id}.

        :param id: The existing file's unique id
        :param file: The updated file

        :return: The file was updated successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/files/',
            str(id)])

        files = {}  # type: Dict[str, BinaryIO]

        files['file'] = file

        resp = self.session.request(
            method='patch',
            url=url,
            files=files,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[DataFile])

    def data_files_delete(
            self,
            id: str) -> bytes:
        """
        If a file is in a corpora and the file is deleted, it will be automatically removed from the corpora.
        If a build job has started before the file was deleted, the file will be used for the build job, even
        though it will no longer be accessible through the API.

        :param id: The existing file's unique id

        :return: The file was deleted successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/files/',
            str(id)])

        resp = self.session.request(
            method='delete',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get_all(self) -> List['TranslationEngine']:
        """
        Send a get request to /api/v1/translation/engines.

        :return: The engines
        """
        url = self.url_prefix + '/api/v1/translation/engines'

        resp = self.session.request(method='get', url=url)

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, TranslationEngine])

    def translation_engines_create(
            self,
            engine_config: 'TranslationEngineConfig') -> bytes:
        """
        ## Parameters
        * **name**: A name to help identify and distinguish the file.
          * Recommendation: Create a multi-part name to distinguish between projects, uses, etc.
          * The name does not have to be unique, as the engine is uniquely identified by the auto-generated id
        * **sourceLanguage**: The source language code (a valid [IETF language tag](https://en.wikipedia.org/wiki/IETF_language_tag) is recommended)
        * **targetLanguage**: The target language code (a valid IETF language tag is recommended)
        * **type**: **SmtTransfer** or **Nmt** or **Echo**
        ### SmtTransfer
        The Statistical Machine Translation Transfer Learning engine is primarily used for translation suggestions.
        Typical endpoints: translate, get-word-graph, train-segment
        ### Nmt
        The Neural Machine Translation engine is primarily used for pretranslations.  It is
        fine tuned from the NLLB-200 from Meta and inherits the 200 language codes. Valid IETF language tags will be converted to an [NLLB-200 code](https://github.com/facebookresearch/flores/tree/main/flores200#languages-in-flores-200), and NLLB will be used as-is.
        Typical endpoints: pretranslate
        ### Echo
        The Echo engine has full coverage of all Nmt and SmtTransfer endpoints. Endpoints like create and build
        return empty responses. Endpoints like translate and get-word-graph echo the sent content back to the user
        in a format that mocks Nmt or Smt. For example, translating a segment "test" with the Echo engine would
        yield a translation response with translation "test". This engine is useful for debugging and testing purposes.
        ## Sample request:
                    
            {
              "name": "myTeam:myProject:myEngine",
              "sourceLanguage": "el",
              "targetLanguage": "en",
              "type": "Nmt"
            }

        :param engine_config: The translation engine configuration (see above)

        :return:
        """
        url = self.url_prefix + '/api/v1/translation/engines'

        data = to_jsonable(
            engine_config,
            expected=[TranslationEngineConfig])


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get(
            self,
            id: str) -> 'TranslationEngine':
        """
        Send a get request to /api/v1/translation/engines/{id}.

        :param id: The translation engine id

        :return: The translation engine
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id)])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationEngine])

    def translation_engines_delete(
            self,
            id: str) -> bytes:
        """
        Send a delete request to /api/v1/translation/engines/{id}.

        :param id: The translation engine id

        :return: The engine was successfully deleted
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id)])

        resp = self.session.request(
            method='delete',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get_queue(
            self,
            engine_type: str) -> 'Queue':
        """
        Send a post request to /api/v1/translation/engines/queues.

        :param engine_type: A valid engine type: SmtTransfer, Nmt, or Echo

        :return: Queue information for the specified engine type
        """
        url = self.url_prefix + '/api/v1/translation/engines/queues'

        data = engine_type


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[Queue])

    def translation_engines_translate(
            self,
            id: str,
            segment: str) -> 'TranslationResult':
        """
        Send a post request to /api/v1/translation/engines/{id}/translate.

        :param id: The translation engine id
        :param segment: The source segment

        :return: The translation result
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/translate'])

        data = segment


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationResult])

    def translation_engines_translate_n(
            self,
            id: str,
            n: int,
            segment: str) -> List['TranslationResult']:
        """
        Send a post request to /api/v1/translation/engines/{id}/translate/{n}.

        :param id: The translation engine id
        :param n: The number of translations to generate
        :param segment: The source segment

        :return: The translation results
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/translate/',
            str(n)])

        data = segment


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, TranslationResult])

    def translation_engines_get_word_graph(
            self,
            id: str,
            segment: str) -> 'WordGraph':
        """
        Send a post request to /api/v1/translation/engines/{id}/get-word-graph.

        :param id: The translation engine id
        :param segment: The source segment

        :return: The word graph result
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/get-word-graph'])

        data = segment


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[WordGraph])

    def translation_engines_train_segment(
            self,
            id: str,
            segment_pair: 'SegmentPair') -> bytes:
        """
        What does `SentenceStart` do?

        :param id: The translation engine id
        :param segment_pair: The segment pair

        :return: The engine was trained successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/train-segment'])

        data = to_jsonable(
            segment_pair,
            expected=[SegmentPair])


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_add_corpus(
            self,
            id: str,
            corpus_config: 'TranslationCorpusConfig') -> bytes:
        """
        ## Parameters
        * **name**: A name to help identify and distinguish the corpus from other corpora
          * The name does not have to be unique since the corpus is uniquely identified by an auto-generated id
        * **sourceLanguage**: The source language code
          * Normally, this is the same as the engine sourceLanguage.  This may change for future engines as a means of transfer learning.
        * **targetLanguage**: The target language code
        * **SourceFiles**: The source files associated with the corpus
          * **FileId**: The unique id referencing the uploaded file
          * **TextId**: The client-defined name to associate source and target files.
            * If the TextIds in the SourceFiles and TargetFiles match, they will be used to train the engine.
            * If selected for pretranslation when building, all SourceFiles that have no TargetFile, or lines of text in a SourceFile that have missing or blank lines in the TargetFile will be pretranslated.
            * A TextId should only be used at most once in SourceFiles and in TargetFiles.
            * If the file is a Paratext project, this field should be left blank. Any TextId provided will be ignored.
        * **TargetFiles**: The source files associated with the corpus
          * Same as SourceFiles.  Parallel texts must have a matching TextId.

        :param id: The translation engine id
        :param corpus_config: The corpus configuration (see remarks)

        :return:
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora'])

        data = to_jsonable(
            corpus_config,
            expected=[TranslationCorpusConfig])


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get_all_corpora(
            self,
            id: str) -> List['TranslationCorpus']:
        """
        Send a get request to /api/v1/translation/engines/{id}/corpora.

        :param id: The translation engine id

        :return: The files
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora'])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, TranslationCorpus])

    def translation_engines_update_corpus(
            self,
            id: str,
            corpus_id: str,
            corpus_config: 'TranslationCorpusUpdateConfig') -> 'TranslationCorpus':
        """
        See posting a new corpus for details of use.  Will completely replace corpus' file associations.
        Will not affect jobs already queued or running.  Will not affect existing pretranslations until new build is complete.

        :param id: The translation engine id
        :param corpus_id: The corpus id
        :param corpus_config: The corpus configuration

        :return: The corpus was updated successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora/',
            str(corpus_id)])

        data = to_jsonable(
            corpus_config,
            expected=[TranslationCorpusUpdateConfig])


        resp = self.session.request(
            method='patch',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationCorpus])

    def translation_engines_get_corpus(
            self,
            id: str,
            corpus_id: str) -> 'TranslationCorpus':
        """
        Send a get request to /api/v1/translation/engines/{id}/corpora/{corpusId}.

        :param id: The translation engine id
        :param corpus_id: The corpus id

        :return: The corpus configuration
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora/',
            str(corpus_id)])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationCorpus])

    def translation_engines_delete_corpus(
            self,
            id: str,
            corpus_id: str) -> bytes:
        """
        Removing a corpus will remove all pretranslations associated with that corpus.

        :param id: The translation engine id
        :param corpus_id: The corpus id

        :return: The data file was deleted successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora/',
            str(corpus_id)])

        resp = self.session.request(
            method='delete',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get_all_pretranslations(
            self,
            id: str,
            corpus_id: str,
            text_id: Optional[str] = None) -> List['Pretranslation']:
        """
        Pretranslations are arranged in a list of dictionaries with the following fields per pretranslation:
        * **TextId**: The TextId of the SourceFile defined when the corpus was created.
        * **Refs** (a list of strings): A list of references including:
          * The references defined in the SourceFile per line, if any.
          * An auto-generated reference of `[TextId]:[lineNumber]`, 1 indexed.
        * **Translation**: the text of the pretranslation
                    
        Pretranslations can be filtered by text id if provided.

        :param id: The translation engine id
        :param corpus_id: The corpus id
        :param text_id: The text id (optional)

        :return: The pretranslations
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/corpora/',
            str(corpus_id),
            '/pretranslations'])

        params = {}  # type: Dict[str, str]

        if text_id is not None:
            params['textId'] = text_id

        resp = self.session.request(
            method='get',
            url=url,
            params=params,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, Pretranslation])

    def translation_engines_get_all_builds(
            self,
            id: str) -> List['TranslationBuild']:
        """
        Send a get request to /api/v1/translation/engines/{id}/builds.

        :param id: The translation engine id

        :return: The build jobs
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/builds'])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, TranslationBuild])

    def translation_engines_start_build(
            self,
            id: str,
            build_config: 'TranslationBuildConfig') -> bytes:
        """
        Specify the corpora or textIds to pretranslate.  Even when a corpus or textId
        is selected for pretranslation, only "untranslated" text will be pretranslated:
        that is, segments (lines of text) in the specified corpora or textId's that have
        untranslated text but no translated text. If a corpus is a Paratext project,
        you may flag a subset of books for pretranslation by including their [abbreviations](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs)
        in the textIds parameter. If the engine does not support pretranslation, these fields have no effect.
                    
        The `"options"` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
        A typical use case would be to set `"options"` to `{"max_steps":10}` in order to configure the maximum
        number of training iterations in order to reduce turnaround time for testing purposes.

        :param id: The translation engine id
        :param build_config: The build config (see remarks)

        :return:
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/builds'])

        data = to_jsonable(
            build_config,
            expected=[TranslationBuildConfig])


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def translation_engines_get_build(
            self,
            id: str,
            build_id: str,
            min_revision: Optional[int] = None) -> 'TranslationBuild':
        """
        If the `minRevision` is not defined, the current build, at whatever state it is,
        will be immediately returned.  If `minRevision` is defined, Serval will wait for
        up to 40 seconds for the engine to build to the `minRevision` specified, else
        will timeout.
        A use case is to actively query the state of the current build, where the subsequent
        request sets the `minRevision` to the returned `revision` + 1 and timeouts are handled gracefully.
        This method should use request throttling.
        Note: Within the returned build, percentCompleted is a value between 0 and 1.

        :param id: The translation engine id
        :param build_id: The build job id
        :param min_revision: The minimum revision

        :return: The build job
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/builds/',
            str(build_id)])

        params = {}  # type: Dict[str, str]

        if min_revision is not None:
            params['minRevision'] = json.dumps(min_revision)

        resp = self.session.request(
            method='get',
            url=url,
            params=params,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationBuild])

    def translation_engines_get_current_build(
            self,
            id: str,
            min_revision: Optional[int] = None) -> 'TranslationBuild':
        """
        See documentation on endpoint /translation/engines/{id}/builds/{id} - "Get a Build Job" for details on using `minRevision`.

        :param id: The translation engine id
        :param min_revision: The minimum revision

        :return: The build job
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/current-build'])

        params = {}  # type: Dict[str, str]

        if min_revision is not None:
            params['minRevision'] = json.dumps(min_revision)

        resp = self.session.request(
            method='get',
            url=url,
            params=params,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[TranslationBuild])

    def translation_engines_cancel_build(
            self,
            id: str) -> bytes:
        """
        Send a post request to /api/v1/translation/engines/{id}/current-build/cancel.

        :param id: The translation engine id

        :return: The build job was cancelled successfully
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/translation/engines/',
            str(id),
            '/current-build/cancel'])

        resp = self.session.request(
            method='post',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def webhooks_get_all(self) -> List['Webhook']:
        """
        Send a get request to /api/v1/hooks.

        :return: The webhooks.
        """
        url = self.url_prefix + '/api/v1/hooks'

        resp = self.session.request(method='get', url=url)

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[list, Webhook])

    def webhooks_create(
            self,
            hook_config: 'WebhookConfig') -> bytes:
        """
        Send a post request to /api/v1/hooks.

        :param hook_config: The webhook configuration.

        :return:
        """
        url = self.url_prefix + '/api/v1/hooks'

        data = to_jsonable(
            hook_config,
            expected=[WebhookConfig])


        resp = self.session.request(
            method='post',
            url=url,
            json=data,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content

    def webhooks_get(
            self,
            id: str) -> 'Webhook':
        """
        Send a get request to /api/v1/hooks/{id}.

        :param id: The webhook id.

        :return: The webhook.
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/hooks/',
            str(id)])

        resp = self.session.request(
            method='get',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return from_obj(
                obj=resp.json(),
                expected=[Webhook])

    def webhooks_delete(
            self,
            id: str) -> bytes:
        """
        Send a delete request to /api/v1/hooks/{id}.

        :param id: The webhook id.

        :return: The webhook was successfully deleted.
        """
        url = "".join([
            self.url_prefix,
            '/api/v1/hooks/',
            str(id)])

        resp = self.session.request(
            method='delete',
            url=url,
        )

        with contextlib.closing(resp):
            resp.raise_for_status()
            return resp.content


# Automatically generated file by swagger_to. DO NOT EDIT OR APPEND ANYTHING!
