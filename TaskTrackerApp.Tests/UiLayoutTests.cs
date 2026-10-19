using System.IO;
using System.Xml.Linq;

namespace TaskTrackerApp.Tests;

/// <summary>
/// Static checks on MainWindow.xaml for the UI items raised during review (consistent page headers,
/// New task button, History page, wheel scrolling over steps). They cannot replace looking at the app,
/// but they stop these regressions from coming back unnoticed.
/// </summary>
public class UiLayoutTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    // The source file's own path is baked in at compile time, so this works wherever the test DLL is copied to.
    private static string RepoFile(string relative, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(here)!, "..", "TaskTrackerApp", relative);
        Assert.That(File.Exists(path), $"missing {path}");
        return Path.GetFullPath(path);
    }

    private XDocument _doc = null!;

    [SetUp]
    public void Load() => _doc = XDocument.Load(RepoFile("MainWindow.xaml"));

    private XElement Named(string name) =>
        _doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static string? Attr(XElement e, string name) => (string?)e.Attribute(name);

    private static XElement FirstTitle(XElement page) =>
        page.Descendants(P + "TextBlock").First(t => (Attr(t, "Style") ?? "").Contains("PageTitle"));

    private static readonly string[] Pages =
        { "TasksTabContent", "DoneLogTabContent", "ProfilesTabContent", "SettingsTabContent", "AboutTabContent" };

    // ---- Consistent page headers

    [TestCaseSource(nameof(Pages))]
    public void EveryPage_StartsWithThePageTitleStyle(string page)
    {
        var title = FirstTitle(Named(page));
        Assert.That(Attr(title, "Text"), Is.Not.Empty);
    }

    [TestCaseSource(nameof(Pages))]
    public void EveryPage_HasASubtitleRightBelowTheTitle(string page)
    {
        var el = Named(page);
        Assert.That(el.Descendants(P + "TextBlock").Any(t => (Attr(t, "Style") ?? "").Contains("PageSubtitle")), Is.True);
    }

    [Test]
    public void PageTitleTexts_AreTheFiveTabNames()
    {
        var titles = Pages.Select(p => Attr(FirstTitle(Named(p)), "Text")).ToList();
        Assert.That(titles, Is.EquivalentTo(new[] { "Tasks", "History", "Profiles", "Settings", "About" }));
    }

    [Test]
    public void PageFrameTokens_AreDefinedOnce_AndAddUp()
    {
        var controls = XDocument.Load(RepoFile(Path.Combine("Themes", "Controls.xaml")));
        string Value(string key) => controls.Descendants().Single(e => (string?)e.Attribute(X + "Key") == key).Value;

        Assert.That(Value("PageContentWidth"), Is.EqualTo("1200"));
        Assert.That(Value("PageGutter"), Is.EqualTo("28,16,28,28"));
        // content + a 28 gutter on each side
        Assert.That(double.Parse(Value("PageMaxWidth")), Is.EqualTo(double.Parse(Value("PageContentWidth")) + 2 * 28));
    }

    [TestCase("DoneLogTabContent")]
    [TestCase("ProfilesTabContent")]
    [TestCase("SettingsTabContent")]
    [TestCase("AboutTabContent")]
    public void ScrollingPages_UseTheSharedCentredColumn(string page)
    {
        var host = FirstTitle(Named(page)).Parent!;
        Assert.That(Attr(host, "Margin"), Is.EqualTo("{StaticResource PageGutter}"));
        Assert.That(Attr(host, "MaxWidth"), Is.EqualTo("{StaticResource PageContentWidth}"));
        Assert.That(Attr(host, "HorizontalAlignment"), Is.EqualTo("Stretch"), "stretch + MaxWidth = centred");
    }

    [Test]
    public void TasksPage_UsesTheSameColumn_AndGutterAsTheOtherPages()
    {
        var tasks = Named("TasksTabContent");
        Assert.That(Attr(tasks, "MaxWidth"), Is.EqualTo("{StaticResource PageMaxWidth}"));
        Assert.That(Attr(tasks, "HorizontalAlignment"), Is.EqualTo("Stretch"));
        Assert.That(tasks.Descendants(P + "Grid").Any(g => Attr(g, "Margin") == "{StaticResource PageGutter}"), Is.True);
    }

    [Test]
    public void NoPage_HardCodesItsOwnWidthOrAlignment()
    {
        var text = File.ReadAllText(RepoFile("MainWindow.xaml"));
        Assert.That(text, Does.Not.Contain("MaxWidth=\"1056\""));
        Assert.That(text, Does.Not.Contain("MaxWidth=\"760\""));
        Assert.That(text, Does.Not.Contain("Margin=\"28,16,28,28\""));
        Assert.That(text, Does.Not.Contain("Margin=\"32,24,32,24\""));
    }

    [Test]
    public void AboutPage_Scrolls()
    {
        Assert.That(Named("AboutTabContent").Descendants(P + "ScrollViewer").Any(), Is.True);
    }

    [Test]
    public void SharedPageStyles_AreDefined()
    {
        var controls = XDocument.Load(RepoFile(Path.Combine("Themes", "Controls.xaml")));
        var keys = controls.Descendants(P + "Style").Select(s => (string?)s.Attribute(X + "Key")).ToList();
        Assert.That(keys, Is.SupersetOf(new[] { "PageTitle", "PageSubtitle", "SectionHeader", "FieldLabel" }));
    }

    // ---- New task button

    [Test]
    public void NewTaskButton_DoesNotStretchToTheHeaderHeight()
    {
        var button = Named("NewTaskButton");
        Assert.That(Attr(button, "VerticalAlignment"), Is.EqualTo("Top"));
        Assert.That(Attr(button, "HorizontalAlignment"), Is.EqualTo("Right"));
    }

    // ---- History tab

    [Test]
    public void SidebarItem_IsCalledHistory_WithTheHistoryIcon_AndNoOldName()
    {
        var nav = Named("NavDoneLog");
        Assert.That(Attr(nav, "ToolTip"), Is.EqualTo("History"));
        Assert.That(Attr(nav, "AutomationProperties.Name"), Is.EqualTo("History"));
        Assert.That(nav.Descendants(P + "TextBlock").Select(t => Attr(t, "Text")), Does.Contain("").Or.Contain("&#xE81C;"));
        Assert.That(File.ReadAllText(RepoFile("MainWindow.xaml")), Does.Not.Contain("Done log\""));
    }

    [Test]
    public void SidebarOrder_IsTasksThenHistory()
    {
        var nav = _doc.Descendants(P + "RadioButton").Select(r => (string?)r.Attribute(X + "Name")).ToList();
        Assert.That(nav.IndexOf("NavDoneLog"), Is.EqualTo(nav.IndexOf("NavTasks") + 1));
    }

    [Test]
    public void HistoryCards_ShowStatus_StepProgress_AndNoCheckBeforeTheTitle()
    {
        var card = Named("DoneLogTabContent").Descendants(P + "Border").First(b => Attr(b, "MouseLeftButtonUp") == "HistoryCard_Click");
        var bindings = card.Descendants().SelectMany(e => e.Attributes()).Select(a => a.Value).ToList();

        Assert.That(bindings, Does.Contain("{Binding Task.State}"), "status shown");
        Assert.That(bindings, Does.Contain("{Binding StepsText}"), "2/3 Steps shown");
        Assert.That(bindings.Any(b => b.Contains("TaskDone")), Is.False, "no check bound to TaskDone");

        // the only check-mark glyph left in a card belongs to a step line (inside the nested steps list)
        var steps = card.Descendants(P + "ItemsControl").Single(i => Attr(i, "ItemsSource") == "{Binding Steps}");
        var glyphsOutsideSteps = card.Descendants(P + "TextBlock")
            .Where(t => Attr(t, "Text") == "" && !t.Ancestors().Contains(steps));
        Assert.That(glyphsOutsideSteps, Is.Empty);
    }

    [Test]
    public void HistoryCards_AreKeyboardAndMouseOperable()
    {
        var card = Named("DoneLogTabContent").Descendants(P + "Border").First(b => Attr(b, "MouseLeftButtonUp") == "HistoryCard_Click");

        Assert.That(Attr(card, "KeyDown"), Is.EqualTo("HistoryCard_KeyDown"));
        Assert.That(Attr(card, "Focusable"), Is.EqualTo("True"));
        Assert.That(Attr(card, "AutomationProperties.Name"), Is.Not.Empty);
        Assert.That(card.Descendants(P + "Trigger").Any(t => Attr(t, "Property") == "IsMouseOver"), Is.True, "hover state");
        Assert.That(card.Descendants(P + "Trigger").Any(t => Attr(t, "Property") == "IsKeyboardFocused"), Is.True, "focus state");
    }

    [Test]
    public void HistoryCardTitle_TruncatesInsteadOfWideningTheCard()
    {
        var title = Named("DoneLogTabContent").Descendants(P + "TextBlock").First(t => Attr(t, "Text") == "{Binding Task.Title}");
        Assert.That(Attr(title, "TextTrimming"), Is.EqualTo("CharacterEllipsis"));
        Assert.That(title.Parent!.Name.LocalName, Is.EqualTo("Grid"));
    }

    [Test]
    public void HistoryEmptyState_HasTitleAndHint()
    {
        var empty = Named("DoneLogEmptyText");
        Assert.That(empty.Descendants(P + "TextBlock").Count(), Is.GreaterThanOrEqualTo(3));
    }

    // ---- Mouse wheel over the steps list

    [Test]
    public void StepsList_ForwardsTheMouseWheelToTheForm()
    {
        var list = Named("StepsListView");
        Assert.That(Attr(list, "PreviewMouseWheel"), Is.EqualTo("StepsListView_PreviewMouseWheel"));
        Assert.That(File.ReadAllText(RepoFile("MainWindow.xaml.cs")), Does.Contain("void StepsListView_PreviewMouseWheel"));
    }

    // ---- Accessibility names added in the review

    [TestCase("StepCheckBox")]
    [TestCase("StepTextBox")]
    [TestCase("FilterStateToDo")]
    [TestCase("FilterStateInProgress")]
    [TestCase("FilterStateDone")]
    public void InteractiveControls_HaveAccessibleNames(string name)
    {
        Assert.That(Attr(Named(name), "AutomationProperties.Name"), Is.Not.Empty);
    }

    [Test]
    public void HistoryStatusBadge_BackgroundComesFromTheStyle_SoStateColoursApply()
    {
        // a locally set Background would beat the style's DataTriggers and the badge would stay grey
        var badge = Named("DoneLogTabContent").Descendants(P + "Border")
            .First(b => b.Descendants(P + "TextBlock").Any(t => Attr(t, "Text") == "{Binding Task.State}")
                        && b.Elements(P + "Border.Style").Any());
        Assert.That(Attr(badge, "Background"), Is.Null);
        Assert.That(badge.Descendants(P + "Setter").Any(s => Attr(s, "Property") == "Background"), Is.True);
    }

    // ---- Packaging

    [Test]
    public void StartupTaskExtension_NamesTheExeInsideThePackageFolder()
    {
        // The packaging project rewrites only Application/@Executable to TaskTrackerApp\TaskTrackerApp.exe;
        // an extension that says plain TaskTrackerApp.exe makes MakeAppx fail in CI.
        var manifest = XDocument.Load(RepoFile(Path.Combine("..", "TaskTrackerApp.Packaging", "AppxManifest.xml")));
        var ext = manifest.Descendants()
            .Where(e => e.Name.LocalName == "Extension" && (string?)e.Attribute("Category") == "windows.startupTask")
            .ToList();

        Assert.That(ext, Has.Count.EqualTo(1));
        Assert.That((string?)ext[0].Attribute("Executable"), Is.EqualTo(@"TaskTrackerApp\TaskTrackerApp.exe"));
    }
}
