using System.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;

namespace PointlessWaymarks.PhotoMetadataBasicsGui.Controls;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class TagsEditorContext : IHasChanges, IHasValidationIssues,
    ICheckForChangesAndValidation
{
    private TagsEditorContext()
    {
        HelpText =
            "Comma separated tags - only a-z 0-9  - [space] are valid, each tag must be less than 200 characters long.";

        PropertyChanged += OnPropertyChanged;
    }

    public string HelpText { get; set; }
    public string Tags { get; set; } = string.Empty;
    public List<string> TagsReference { get; set; } = [];
    public string TagsReferenceString { get; set; } = string.Empty;
    public string TagsValidationMessage { get; set; } = string.Empty;

    public void CheckForChangesAndValidationIssues()
    {
        Tags = SlugTagTools.CreateRelaxedInputSpacedString(true, Tags, [',', ' ', '-', '_']).ToLower();

        HasChanges = !TagsList().SequenceEqual(TagsReference);
    }

    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }

    public static TagsEditorContext CreateInstance()
    {
        var newItem = new TagsEditorContext();

        newItem.CheckForChangesAndValidationIssues();

        return newItem;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(TagsReference)))
            TagsReferenceString = SlugTagTools.TagListJoinToSpacedString(TagsReference);

        if (!e.PropertyName.Contains("HasChanges") && !e.PropertyName.Contains("Validation"))
            CheckForChangesAndValidationIssues();
    }

    public List<string> TagsList()
    {
        return string.IsNullOrWhiteSpace(Tags) ? [] : SlugTagTools.TagListParseToSpacedString(Tags);
    }

    public string TagsListString()
    {
        return string.IsNullOrWhiteSpace(Tags) ? string.Empty : SlugTagTools.TagListJoinToSpacedString(TagsList());
    }
}