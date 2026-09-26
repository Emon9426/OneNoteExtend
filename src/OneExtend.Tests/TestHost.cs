using System.IO;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.Theme;

namespace OneExtend.Tests
{
    /// <summary>Loads real grammar/theme data from the repository checkout.</summary>
    public static class TestHost
    {
        private static LanguageRegistry _languages;
        private static ThemeLoader _themes;

        public static string DataRoot => GrammarLoader.LocateDataRoot();

        public static LanguageRegistry Languages
        {
            get
            {
                if (_languages == null)
                {
                    var dir = Path.Combine(DataRoot, "grammars");
                    _languages = new LanguageRegistry(GrammarLoader.LoadDirectory(dir));
                }
                return _languages;
            }
        }

        public static ThemeLoader Themes
        {
            get
            {
                if (_themes == null)
                    _themes = ThemeLoader.LoadDefault(DataRoot);
                return _themes;
            }
        }

        public static Tokenizer TokenizerFor(string languageId)
        {
            return new Tokenizer(Languages.Get(languageId));
        }
    }
}
