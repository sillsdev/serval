@Integration
Feature: TranslationAssist

	@E2E
	Scenario: Get Echo Suggestion
		Given a new Echo engine for John from es to en
		When a text corpora containing 1JN.txt, 2JN.txt, 3JN.txt are added to John's engine in es and en
		Then the translation for John for "Espíritu" should be "Espíritu"

	@E2E
	Scenario: Get Translation Suggestion
		Given a new SmtTransfer engine for John from es to en
		When a text corpora containing 1JN.txt, 2JN.txt, 3JN.txt are added to John's engine in es and en
		And John's engine is built
		Then the translation for John for "Espíritu" should be "spirit"

	@E2E
	Scenario: Get Translation Suggestion from whole Bible
		Given a new SmtTransfer engine for John from es to en
		When a text corpora containing bible.txt are added to John's engine in es and en
		And John's engine is built
		Then the translation for John for "Espíritu" should be "Spirit"

	@E2E
	Scenario: Add training segment
		Given a new SmtTransfer engine for John from es to en
		When a text corpora containing 1JN.txt, 2JN.txt, 3JN.txt are added to John's engine in es and en
		And John's engine is built
		And the translation for John for "ungidos espíritu" is "ungidos spirit"
		And a translation for John is added with "unction spirit" for "ungidos espíritu"
		Then the translation for John for "ungidos espíritu" should be "unction spirit"

	@E2E
	Scenario: Add More Corpus
		Given a new SmtTransfer engine for John from es to en
		When a text corpora containing 3JN.txt are added to John's engine in es and en
		And John's engine is built
		And the translation for John for "verdad mundo" is "truth mundo"
		When a text corpora containing 1JN.txt, 2JN.txt are added to John's engine in es and en
		And John's engine is built
		Then the translation for John for "verdad mundo" should be "truth world"

	@E2E
	Scenario: Get Nmt Pretranslation
		Given a new Nmt engine for John from es to en
		When a text corpora containing MAT.txt, 1JN.txt, 2JN.txt are added to John's engine in es and en
		And a text corpora containing 3JN.txt are added to John's engine in es to translate into en
		And John's engine is built
		Then the pretranslation for John for 3JN.txt starts with "The elder unto the"