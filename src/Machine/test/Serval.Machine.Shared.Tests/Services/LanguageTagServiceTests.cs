namespace Serval.Machine.Shared.Services;

[TestFixture]
public class LanguageTagServiceTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (!Sldr.IsInitialized)
            Sldr.Initialize();
    }

    [Test]
    [TestCase("es", "spa_Latn", Description = "Iso639_1Code")]
    [TestCase("hne", "hne_Deva", Description = "Iso639_3Code")]
    [TestCase("ks-Arab", "kas_Arab", Description = "ScriptCode")]
    [TestCase("srp_Cyrl", "srp_Cyrl", Description = "InvalidLangTag")]
    [TestCase("zh", "zho_Hans", Description = "ChineseNoScript")]
    [TestCase("zh-Hant", "zho_Hant", Description = "ChineseScript")]
    [TestCase("zh-TW", "zho_Hant", Description = "ChineseRegion")]
    [TestCase("cmn", "zho_Hans", Description = "MandarinChineseNoScript")]
    [TestCase("cmn-Hant", "zho_Hant", Description = "MandarinChineseScript")]
    [TestCase("ms", "zsm_Latn", Description = "Macrolanguage")]
    [TestCase("arb", "arb_Arab", Description = "Arabic")]
    [TestCase("pes", "pes_Arab", Description = "IranianPersianNoScript")]
    [TestCase("eng", "eng_Latn", Description = "InsteadOfISO639_1")]
    [TestCase("eng-Latn", "eng_Latn", Description = "DashToUnderscore")]
    [TestCase("kor", "kor_Hang", Description = "KoreanScript")]
    [TestCase("kor_Kore", "kor_Hang", Description = "KoreanScriptCorrection")]
    public void ConvertToFlores200CodeTest(string language, string internalCodeTruth)
    {
        new LanguageTagService().ConvertToFlores200Code(language, out string internalCode);
        Assert.That(internalCode, Is.EqualTo(internalCodeTruth));
    }

    [Test]
    [TestCase("en", "eng_Latn", Flores200Support.LanguageAndScript)]
    [TestCase("ms", "zsm_Latn", Flores200Support.LanguageAndScript)]
    [TestCase("cmn", "zho_Hans", Flores200Support.LanguageAndScript)]
    [TestCase("xyz-Latn", "xyz_Latn", Flores200Support.None)]
    [TestCase("xyz", "xyz", Flores200Support.None)]
    [TestCase("lif-Limb", "lif_Limb", Flores200Support.None)]
    public void GetLanguageInfoAsync(
        string languageCode,
        string? resolvedLanguageCode,
        Flores200Support expectedFlores200Support
    )
    {
        Flores200Support flores200support = new LanguageTagService().ConvertToFlores200Code(
            languageCode,
            out string internalCode
        );
        Assert.Multiple(() =>
        {
            Assert.That(internalCode, Is.EqualTo(resolvedLanguageCode));
            Assert.That(flores200support, Is.EqualTo(expectedFlores200Support));
        });
    }
}
