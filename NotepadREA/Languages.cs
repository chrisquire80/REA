using ScintillaNET;

namespace NotepadREA;

public enum Tok { Comment, Keyword, Keyword2, String, Number, Operator, Preprocessor, Tag, Attribute, Default }

/// <summary>Definizione di un linguaggio: lexer Lexilla, estensioni, parole chiave, mappa stile→token.</summary>
public sealed class Language
{
    public required string Name { get; init; }
    public required string Lexer { get; init; }
    public string[] Extensions { get; init; } = [];
    public string Keywords0 { get; init; } = "";
    public string Keywords1 { get; init; } = "";
    public string? LineComment { get; init; }
    public (string Open, string Close)? BlockComment { get; init; }
    public Dictionary<int, Tok> Styles { get; init; } = [];
    public bool Fold { get; init; } = true;

    public override string ToString() => Name;
}

public static class Languages
{
    // Numeri di stile: SciLexer.h (Lexilla), lo stesso motore linkato staticamente in notepad++.exe.
    static readonly Dictionary<int, Tok> CppStyles = new()
    {
        [1] = Tok.Comment, [2] = Tok.Comment, [3] = Tok.Comment, [15] = Tok.Comment, [17] = Tok.Comment, [18] = Tok.Comment,
        [4] = Tok.Number, [5] = Tok.Keyword, [16] = Tok.Keyword2, [6] = Tok.String, [7] = Tok.String, [12] = Tok.String,
        [13] = Tok.String, [14] = Tok.String, [20] = Tok.String, [9] = Tok.Preprocessor, [10] = Tok.Operator,
    };

    static readonly Dictionary<int, Tok> MarkupStyles = new()
    {
        [1] = Tok.Tag, [2] = Tok.Tag, [11] = Tok.Tag, [3] = Tok.Attribute, [4] = Tok.Attribute, [5] = Tok.Number,
        [6] = Tok.String, [7] = Tok.String, [9] = Tok.Comment, [10] = Tok.Preprocessor, [12] = Tok.Preprocessor,
        [13] = Tok.Preprocessor, [17] = Tok.Preprocessor, [18] = Tok.Preprocessor, [21] = Tok.Preprocessor,
        // JavaScript incorporato (SCE_HJ_*)
        [42] = Tok.Comment, [43] = Tok.Comment, [44] = Tok.Comment, [45] = Tok.Number, [47] = Tok.Keyword,
        [48] = Tok.String, [49] = Tok.String, [50] = Tok.Operator,
    };

    const string CKeywords = "auto break case char const continue default do double else enum extern float for goto if inline int long register restrict return short signed sizeof static struct switch typedef union unsigned void volatile while";
    const string CppKeywords = CKeywords + " alignas alignof and asm bool catch class concept consteval constexpr constinit const_cast co_await co_return co_yield decltype delete dynamic_cast explicit export false friend mutable namespace new noexcept not nullptr operator or private protected public reinterpret_cast requires static_assert static_cast template this thread_local throw true try typeid typename using virtual wchar_t xor";
    const string CsKeywords = "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while var async await record init required get set value yield partial where when nameof dynamic global";
    const string JavaKeywords = "abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally float for goto if implements import instanceof int interface long native new null package private protected public return short static strictfp super switch synchronized this throw throws transient try void volatile while true false var record sealed permits yield";
    const string JsKeywords = "async await break case catch class const continue debugger default delete do else export extends false finally for function if import in instanceof let new null of return static super switch this throw true try typeof undefined var void while with yield interface type enum implements private protected public readonly abstract as any boolean number string never unknown declare namespace module keyof";

    public static readonly Language Normal = new() { Name = "Testo normale", Lexer = "null", Extensions = [".txt", ".log"], Fold = false };

    public static readonly Language[] All =
    [
        Normal,
        new() { Name = "C", Lexer = "cpp", Extensions = [".c", ".h"], Keywords0 = CKeywords, LineComment = "//", BlockComment = ("/*", "*/"), Styles = CppStyles },
        new() { Name = "C++", Lexer = "cpp", Extensions = [".cpp", ".cxx", ".cc", ".hpp", ".hxx", ".hh", ".ino"], Keywords0 = CppKeywords, LineComment = "//", BlockComment = ("/*", "*/"), Styles = CppStyles },
        new() { Name = "C#", Lexer = "cpp", Extensions = [".cs", ".csx"], Keywords0 = CsKeywords, LineComment = "//", BlockComment = ("/*", "*/"), Styles = CppStyles },
        new() { Name = "Java", Lexer = "cpp", Extensions = [".java"], Keywords0 = JavaKeywords, LineComment = "//", BlockComment = ("/*", "*/"), Styles = CppStyles },
        new() { Name = "JavaScript / TypeScript", Lexer = "cpp", Extensions = [".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx"], Keywords0 = JsKeywords, LineComment = "//", BlockComment = ("/*", "*/"), Styles = CppStyles },
        new() { Name = "JSON", Lexer = "json", Extensions = [".json", ".jsonc", ".geojson"], Keywords0 = "true false null", LineComment = "//",
            Styles = new() { [1] = Tok.Number, [2] = Tok.String, [3] = Tok.String, [4] = Tok.Attribute, [5] = Tok.Preprocessor, [6] = Tok.Comment, [7] = Tok.Comment, [8] = Tok.Operator, [11] = Tok.Keyword, [13] = Tok.Preprocessor } },
        new() { Name = "Python", Lexer = "python", Extensions = [".py", ".pyw", ".pyi"], LineComment = "#",
            Keywords0 = "False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case",
            Keywords1 = "print len range str int float list dict set tuple open self super isinstance enumerate zip map filter sorted",
            Styles = new() { [1] = Tok.Comment, [12] = Tok.Comment, [2] = Tok.Number, [3] = Tok.String, [4] = Tok.String, [6] = Tok.String, [7] = Tok.String, [5] = Tok.Keyword, [14] = Tok.Keyword2, [8] = Tok.Keyword2, [9] = Tok.Keyword2, [10] = Tok.Operator, [15] = Tok.Preprocessor } },
        new() { Name = "HTML", Lexer = "hypertext", Extensions = [".html", ".htm", ".xhtml", ".shtml", ".php", ".asp", ".aspx", ".vue"], BlockComment = ("<!--", "-->"),
            Keywords0 = "a abbr address area article aside audio b base bdi bdo blockquote body br button canvas caption cite code col colgroup data datalist dd del details dfn dialog div dl dt em embed fieldset figcaption figure footer form h1 h2 h3 h4 h5 h6 head header hr html i iframe img input ins kbd label legend li link main map mark meta meter nav noscript object ol optgroup option output p param picture pre progress q rp rt ruby s samp script section select small source span strong style sub summary sup svg table tbody td template textarea tfoot th thead time title tr track u ul var video wbr class id href src type name value rel style alt width height lang charset content onclick onload",
            Keywords1 = JsKeywords, Styles = MarkupStyles },
        new() { Name = "XML", Lexer = "xml", Extensions = [".xml", ".xsd", ".xsl", ".xslt", ".config", ".csproj", ".props", ".targets", ".resx", ".svg", ".xaml", ".plist", ".udl"], BlockComment = ("<!--", "-->"), Styles = MarkupStyles },
        new() { Name = "CSS", Lexer = "css", Extensions = [".css", ".scss", ".less"], BlockComment = ("/*", "*/"),
            Keywords0 = "color background background-color border margin padding width height display position top left right bottom font font-size font-family font-weight text-align flex grid z-index overflow opacity transition transform content",
            Styles = new() { [1] = Tok.Tag, [2] = Tok.Keyword2, [3] = Tok.Keyword2, [5] = Tok.Operator, [6] = Tok.Keyword, [8] = Tok.String, [9] = Tok.Comment, [10] = Tok.Attribute, [11] = Tok.Preprocessor, [13] = Tok.String, [14] = Tok.String } },
        new() { Name = "SQL", Lexer = "sql", Extensions = [".sql"], LineComment = "--", BlockComment = ("/*", "*/"),
            Keywords0 = "add all alter and as asc between by case check column constraint create database default delete desc distinct drop else end exec exists foreign from full group having in index inner insert into is join key left like limit not null on or order outer primary procedure references right select set table then top truncate union unique update values view when where with begin commit rollback transaction declare int varchar nvarchar char date datetime decimal float bit count sum avg min max cast convert",
            Styles = new() { [1] = Tok.Comment, [2] = Tok.Comment, [3] = Tok.Comment, [15] = Tok.Comment, [4] = Tok.Number, [5] = Tok.Keyword, [16] = Tok.Keyword2, [6] = Tok.String, [7] = Tok.String, [10] = Tok.Operator } },
        new() { Name = "Batch", Lexer = "batch", Extensions = [".bat", ".cmd"], LineComment = "REM ",
            Keywords0 = "rem set if exist errorlevel for in do break call copy chcp cd chdir choice cls country ctty date del erase dir echo exit goto loadfix loadhigh mkdir md move path pause prompt rename ren rmdir rd shift time type ver verify vol com con lpt nul defined not errorlevel cmdextversion start setlocal endlocal pushd popd title",
            Styles = new() { [1] = Tok.Comment, [2] = Tok.Keyword, [3] = Tok.Preprocessor, [4] = Tok.Keyword2, [5] = Tok.Keyword2, [6] = Tok.Attribute, [7] = Tok.Operator } },
        new() { Name = "PowerShell", Lexer = "powershell", Extensions = [".ps1", ".psm1", ".psd1"], LineComment = "#", BlockComment = ("<#", "#>"),
            Keywords0 = "begin break catch continue data do dynamicparam else elseif end exit filter finally for foreach from function if in param process return switch throw trap try until where while",
            Keywords1 = "write-host write-output get-item get-childitem set-content get-content new-item remove-item select-object where-object foreach-object invoke-command start-process",
            Styles = new() { [1] = Tok.Comment, [13] = Tok.Comment, [14] = Tok.Comment, [2] = Tok.String, [3] = Tok.String, [15] = Tok.String, [16] = Tok.String, [4] = Tok.Number, [5] = Tok.Attribute, [6] = Tok.Operator, [8] = Tok.Keyword, [9] = Tok.Keyword2, [10] = Tok.Keyword2 } },
        new() { Name = "INI / Properties", Lexer = "props", Extensions = [".ini", ".inf", ".properties", ".cfg", ".conf", ".reg", ".toml", ".editorconfig"], LineComment = ";",
            Styles = new() { [1] = Tok.Comment, [2] = Tok.Keyword, [3] = Tok.Operator, [4] = Tok.Preprocessor, [5] = Tok.Attribute } },
        new() { Name = "YAML", Lexer = "yaml", Extensions = [".yml", ".yaml"], LineComment = "#", Keywords0 = "true false yes no null",
            Styles = new() { [1] = Tok.Comment, [2] = Tok.Attribute, [3] = Tok.Keyword, [4] = Tok.Number, [5] = Tok.Preprocessor, [6] = Tok.Preprocessor, [9] = Tok.Operator } },
        new() { Name = "Markdown", Lexer = "markdown", Extensions = [".md", ".markdown"], Fold = false,
            Styles = new() { [2] = Tok.Keyword2, [3] = Tok.Keyword2, [4] = Tok.Attribute, [5] = Tok.Attribute, [6] = Tok.Keyword, [7] = Tok.Keyword, [8] = Tok.Keyword, [9] = Tok.Keyword, [10] = Tok.Keyword, [11] = Tok.Keyword, [12] = Tok.Preprocessor, [13] = Tok.Operator, [14] = Tok.Operator, [15] = Tok.Comment, [18] = Tok.Number, [19] = Tok.String, [20] = Tok.String, [21] = Tok.String } },
        new() { Name = "Shell (bash)", Lexer = "bash", Extensions = [".sh", ".bash", ".zsh"], LineComment = "#",
            Keywords0 = "if then else elif fi case esac for while until do done in function select return exit break continue local export readonly declare echo cd source alias unset shift test",
            Styles = new() { [2] = Tok.Comment, [3] = Tok.Number, [4] = Tok.Keyword, [5] = Tok.String, [6] = Tok.String, [7] = Tok.Operator, [9] = Tok.Attribute, [10] = Tok.Attribute, [11] = Tok.String, [12] = Tok.Preprocessor, [13] = Tok.Preprocessor } },
    ];

    public static Language FromPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return Normal;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (name is "makefile" or ".bashrc" or ".profile") return All.First(l => l.Lexer == "bash");
        return All.FirstOrDefault(l => l.Extensions.Contains(ext)) ?? Normal;
    }

    public static void Apply(Scintilla sci, Language lang, Theme theme, string fontName, int fontSize)
    {
        sci.StyleResetDefault();
        var def = sci.Styles[Style.Default];
        def.Font = fontName;
        def.Size = fontSize;
        def.BackColor = theme.Back;
        def.ForeColor = theme.Fore;
        sci.StyleClearAll();

        sci.Styles[Style.LineNumber].BackColor = theme.MarginBack;
        sci.Styles[Style.LineNumber].ForeColor = theme.MarginFore;
        sci.Styles[Style.IndentGuide].ForeColor = theme.MarginFore;
        sci.Styles[Style.BraceLight].ForeColor = Color.Red;
        sci.Styles[Style.BraceLight].Bold = true;
        sci.Styles[Style.BraceBad].ForeColor = Color.Magenta;

        sci.LexerName = lang.Lexer;
        sci.SetKeywords(0, lang.Keywords0);
        sci.SetKeywords(1, lang.Keywords1);

        foreach (var (style, tok) in lang.Styles)
        {
            var s = sci.Styles[style];
            s.ForeColor = tok switch
            {
                Tok.Comment => theme.Comment,
                Tok.Keyword => theme.Keyword,
                Tok.Keyword2 => theme.Keyword2,
                Tok.String => theme.String,
                Tok.Number => theme.Number,
                Tok.Operator => theme.Operator,
                Tok.Preprocessor => theme.Preprocessor,
                Tok.Tag => theme.Tag,
                Tok.Attribute => theme.Attribute,
                _ => theme.Fore,
            };
            if (tok is Tok.Keyword or Tok.Operator or Tok.Tag) s.Bold = true;
            if (tok is Tok.Comment && lang.Lexer != "markdown") s.Italic = false;
        }

        sci.SetProperty("fold", lang.Fold ? "1" : "0");
        sci.SetProperty("fold.compact", "0");
        sci.SetProperty("fold.html", "1");
        sci.SetProperty("fold.comment", "1");
        sci.SetProperty("fold.preprocessor", "1");
        sci.SetProperty("lexer.cpp.track.preprocessor", "0");

        sci.CaretForeColor = theme.Caret;
        sci.CaretLineBackColor = theme.CaretLine;
        sci.SelectionBackColor = theme.Selection;
        sci.SetFoldMarginColor(true, theme.MarginBack);
        sci.SetFoldMarginHighlightColor(true, theme.MarginBack);
        for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
        {
            sci.Markers[i].SetForeColor(theme.MarginBack);
            sci.Markers[i].SetBackColor(theme.MarginFore);
        }
        sci.Colorize(0, -1);
    }
}
