using System.ComponentModel;
using PointlessWaymarks.CmsWpfControls.ContentList;
using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.CmsWpfControls.ListFilterBuilder;

[NotifyPropertyChanged]
public partial class DateTimeListFilterFieldBuilder
{
    public DateTimeListFilterFieldBuilder()
    {
        PropertyChanged += OnPropertyChanged;
    }

    public bool EnableDateTimeTwo { get; set; }
    public required string FieldTitle { get; set; }
    public bool Not { get; set; }
    public List<string> OperatorChoices { get; set; } = ["==", ">", ">=", "<", "<="];
    public string SelectedOperatorOne { get; set; } = "==";
    public string? SelectedOperatorTwo { get; set; } = "<";
    public bool ShowDateTimeOneTextWarning { get; set; }
    public bool ShowDateTimeTwoTextWarning { get; set; }
    public bool UserDateTimeOneTextConverts { get; set; }
    public string UserDateTimeOneTranslation { get; set; } = string.Empty;
    public string UserDateTimeTextOne { get; set; } = string.Empty;
    public string UserDateTimeTextTwo { get; set; } = string.Empty;
    public bool UserDateTimeTwoTextConverts { get; set; }
    public string UserDateTimeTwoTranslation { get; set; } = string.Empty;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName?.Equals(nameof(UserDateTimeTextOne)) ?? false)
        {
            var parseOneResults = TextParses(UserDateTimeTextOne, SelectedOperatorOne);
            UserDateTimeOneTextConverts = parseOneResults.Item1;
            UserDateTimeOneTranslation = parseOneResults.Item2;
            ShowDateTimeOneTextWarning =
                !string.IsNullOrWhiteSpace(UserDateTimeTextOne) && !UserDateTimeOneTextConverts;
        }

        if ((e.PropertyName?.Equals(nameof(UserDateTimeTextOne)) ?? false) ||
            (e.PropertyName?.Equals(nameof(SelectedOperatorOne)) ?? false))
            EnableDateTimeTwo = UserDateTimeOneTextConverts && SelectedOperatorOne != "==";

        if (e.PropertyName?.Equals(nameof(UserDateTimeTextTwo)) ?? false)
        {
            var parseTwoResults = TextParses(UserDateTimeTextTwo, SelectedOperatorTwo ?? "");
            UserDateTimeTwoTextConverts = parseTwoResults.Item1;
            UserDateTimeTwoTranslation = parseTwoResults.Item2;
            ShowDateTimeTwoTextWarning = EnableDateTimeTwo && (!UserDateTimeOneTextConverts ||
                                                               (!string.IsNullOrWhiteSpace(UserDateTimeTextTwo) &&
                                                                !UserDateTimeTwoTextConverts));
        }
    }

    private (bool, string) TextParses(string searchString, string operatorChoice)
    {
        if (!ContentListSearchFunctions.TryParseDateTimeSearch(searchString, out var parsed))
            return (false, string.Empty);

        if (parsed.Type == ContentListSearchFunctions.ParsedDateTimeType.DateRange)
        {
            switch (operatorChoice)
            {
                case "":
                case "==":
                    return (true, $">= {parsed.Start} and < {parsed.End}");
                case "!=":
                    return (true, $"< {parsed.Start} and >= {parsed.End}");
                case ">":
                    return (true, $"> {parsed.End}");
                case ">=":
                    return (true, $">= {parsed.End}");
                case "<":
                    return (true, $"< {parsed.End}");
                case "<=":
                    return (true, $"<= {parsed.End}");
            }
        }

        if (parsed.Type == ContentListSearchFunctions.ParsedDateTimeType.Date)
        {
            return (true, $"{operatorChoice} {parsed.ExactDateTime.Date}");
        }

        if (parsed.Type == ContentListSearchFunctions.ParsedDateTimeType.DateTime)
        {
            return (true, $"{operatorChoice} {parsed.ExactDateTime}");
        }

        if (parsed.Type == ContentListSearchFunctions.ParsedDateTimeType.Time)
        {
            return (true, $"{operatorChoice} {parsed.Time}");
        }

        return (false, string.Empty);
    }
}