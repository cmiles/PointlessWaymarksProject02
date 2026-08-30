using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CmsWpfControls.HelpDisplay;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.MarkdownDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDataEntry;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemEditor;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class WorkoutItemEditorContext : IHasChanges, IHasValidationIssues, ICheckForChangesAndValidation
{
    public EventHandler? RequestContentEditorWindowClose;

    public WorkoutItemEditorContext(StatusControlContext statusContext, WorkoutItem dbEntry)
    {
        StatusContext = statusContext;
        BuildCommands();
        DbEntry = dbEntry;
        PropertyChanged += OnPropertyChanged;
    }

    public ConversionDataEntryContext<int?> CaloriesEntry { get; set; } = null!;
    public ConversionDataEntryContext<int> ClimbFeetEntry { get; set; } = null!;
    public WorkoutItem DbEntry { get; set; }
    public ConversionDataEntryContext<int> DescentFeetEntry { get; set; } = null!;
    public ConversionDataEntryContext<double?> DistanceMilesEntry { get; set; } = null!;
    public ConversionDataEntryContext<int> DurationMinutesEntry { get; set; } = null!;
    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    public HelpDisplayContext HelpContext { get; set; } = null!;
    public StringDataEntryContext NoteEntry { get; set; } = null!;
    public StatusControlContext StatusContext { get; set; }
    public StringDataEntryContext WorkoutByEntry { get; set; } = null!;
    public ConversionDataEntryContext<DateTime> WorkoutOnEntry { get; set; } = null!;
    public StringDataEntryContext WorkoutTypeEntry { get; set; } = null!;

    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this);
        HasValidationIssues = PropertyScanners.ChildPropertiesHaveValidationIssues(this);
    }

    public static async Task<WorkoutItemEditorContext> CreateInstance(StatusControlContext? statusContext,
        WorkoutItem? toLoad = null)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var initialItem = toLoad ?? new WorkoutItem
        {
            ContentId = Guid.NewGuid(),
            WorkoutOn = DateTime.Now,
            WorkoutBy = UserSettingsSingleton.CurrentSettings().DefaultCreatedBy,
            WorkoutType = string.Empty,
            Note = string.Empty
        };

        var newContext = new WorkoutItemEditorContext(factoryStatusContext, initialItem);
        await newContext.LoadData(toLoad);
        return newContext;
    }

    private WorkoutItem CurrentStateToWorkoutItem()
    {
        return new WorkoutItem
        {
            Id = DbEntry.Id,
            ContentId = DbEntry.ContentId,
            WorkoutOn = WorkoutOnEntry.UserValue,
            WorkoutType = WorkoutTypeEntry.UserValue.TrimNullToEmpty(),
            WorkoutBy = WorkoutByEntry.UserValue.TrimNullToEmpty(),
            DurationMinutes = DurationMinutesEntry.UserValue,
            DistanceMiles = DistanceMilesEntry.UserValue,
            ClimbFeet = ClimbFeetEntry.UserValue,
            DescentFeet = DescentFeetEntry.UserValue,
            Calories = CaloriesEntry.UserValue,
            Note = NoteEntry.UserValue.TrimNullToEmpty()
        };
    }

    public async Task LoadData(WorkoutItem? toLoad)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        DbEntry = toLoad ?? new WorkoutItem
        {
            ContentId = Guid.NewGuid(),
            WorkoutOn = DateTime.Now,
            WorkoutBy = UserSettingsSingleton.CurrentSettings().DefaultCreatedBy,
            WorkoutType = string.Empty,
            Note = string.Empty
        };

        WorkoutOnEntry =
            await ConversionDataEntryContext<DateTime>.CreateInstance(ConversionDataEntryHelpers.DateTimeConversion);
        WorkoutOnEntry.Title = "Workout Date/Time";
        WorkoutOnEntry.HelpText = "Date and time for the workout";
        WorkoutOnEntry.ReferenceValue = DbEntry.WorkoutOn;
        WorkoutOnEntry.UserText = DbEntry.WorkoutOn.ToString("MM/dd/yyyy h:mm:ss tt");

        WorkoutTypeEntry = StringDataEntryContext.CreateInstance();
        WorkoutTypeEntry.Title = "Workout Type";
        WorkoutTypeEntry.HelpText = "Type of workout (e.g. Run, Bike, Hike, Walk, Strength, Yoga, Swim)";
        WorkoutTypeEntry.ReferenceValue = DbEntry.WorkoutType;
        WorkoutTypeEntry.UserValue = DbEntry.WorkoutType;
        WorkoutTypeEntry.ValidationFunctions =
            [x => Task.FromResult(new IsValid(!string.IsNullOrWhiteSpace(x), "Workout Type is required"))];

        WorkoutByEntry = StringDataEntryContext.CreateInstance();
        WorkoutByEntry.Title = "Workout By";
        WorkoutByEntry.HelpText = "Person who performed the workout";
        WorkoutByEntry.ReferenceValue = DbEntry.WorkoutBy;
        WorkoutByEntry.UserValue = DbEntry.WorkoutBy;
        WorkoutByEntry.ValidationFunctions =
            [x => Task.FromResult(new IsValid(!string.IsNullOrWhiteSpace(x), "Workout By is required"))];

        DurationMinutesEntry =
            await ConversionDataEntryContext<int>.CreateInstance(ConversionDataEntryHelpers
                .IntGreaterThanZeroConversion);
        DurationMinutesEntry.Title = "Duration (Minutes)";
        DurationMinutesEntry.HelpText = "Total duration of the workout in minutes";
        DurationMinutesEntry.ReferenceValue = DbEntry.DurationMinutes;
        DurationMinutesEntry.UserText = DbEntry.DurationMinutes.ToString();

        DistanceMilesEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(ConversionDataEntryHelpers
                .DoubleNullableConversion);
        DistanceMilesEntry.Title = "Distance (Miles)";
        DistanceMilesEntry.HelpText = "Optional distance in miles";
        DistanceMilesEntry.ReferenceValue = DbEntry.DistanceMiles;
        DistanceMilesEntry.UserText = DbEntry.DistanceMiles?.ToString("F2") ?? string.Empty;

        ClimbFeetEntry =
            await ConversionDataEntryContext<int>.CreateInstance(ConversionDataEntryHelpers.IntConversion);
        ClimbFeetEntry.Title = "Climb (Feet)";
        ClimbFeetEntry.HelpText = "Total climbing in feet";
        ClimbFeetEntry.ReferenceValue = DbEntry.ClimbFeet;
        ClimbFeetEntry.UserText = DbEntry.ClimbFeet.ToString();

        DescentFeetEntry =
            await ConversionDataEntryContext<int>.CreateInstance(ConversionDataEntryHelpers.IntConversion);
        DescentFeetEntry.Title = "Descent (Feet)";
        DescentFeetEntry.HelpText = "Total descent in feet";
        DescentFeetEntry.ReferenceValue = DbEntry.DescentFeet;
        DescentFeetEntry.UserText = DbEntry.DescentFeet.ToString();

        CaloriesEntry =
            await ConversionDataEntryContext<int?>.CreateInstance(ConversionDataEntryHelpers.IntNullableConversion);
        CaloriesEntry.Title = "Calories";
        CaloriesEntry.HelpText = "Optional calories burned";
        CaloriesEntry.ReferenceValue = DbEntry.Calories;
        CaloriesEntry.UserText = DbEntry.Calories?.ToString() ?? string.Empty;

        NoteEntry = StringDataEntryContext.CreateInstance();
        NoteEntry.Title = "Note";
        NoteEntry.HelpText = "Optional notes or details about the workout";
        NoteEntry.ReferenceValue = DbEntry.Note;
        NoteEntry.UserValue = DbEntry.Note;

        HelpContext = new HelpDisplayContext([
            """
            ### Workout Items

            Workout items record workout details such as date/time, type of workout, duration, distance, elevation gain, calories, and notes.
            """
        ]);

        PropertyScanners.SubscribeToChildHasChangesAndHasValidationIssues(this, CheckForChangesAndValidationIssues);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (!e.PropertyName.Contains("HasChanges") && !e.PropertyName.Contains("Validation"))
            CheckForChangesAndValidationIssues();
    }

    [BlockingCommand]
    public async Task Save()
    {
        await Save(false);
    }

    [BlockingCommand]
    public async Task SaveAndClose()
    {
        await Save(true);
    }

    public async Task Save(bool closeAfterSave)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (HasValidationIssues)
        {
            await StatusContext.ToastError("Please correct validation issues before saving.");
            return;
        }

        var toSave = CurrentStateToWorkoutItem();

        if (string.IsNullOrWhiteSpace(toSave.WorkoutType))
        {
            await StatusContext.ToastError("Workout Type cannot be blank.");
            return;
        }

        if (toSave.DurationMinutes <= 0)
        {
            await StatusContext.ToastError("Duration must be greater than 0.");
            return;
        }

        var isNew = toSave.Id < 1;
        var context = await Db.Context();

        if (isNew)
        {
            await context.WorkoutItems.AddAsync(toSave);
        }
        else
        {
            var existing = await context.WorkoutItems.FirstOrDefaultAsync(x => x.Id == toSave.Id);
            if (existing == null)
            {
                await StatusContext.ToastError($"Could not find existing Workout Item with Id {toSave.Id}");
                return;
            }

            existing.WorkoutOn = toSave.WorkoutOn;
            existing.WorkoutType = toSave.WorkoutType;
            existing.WorkoutBy = toSave.WorkoutBy;
            existing.DurationMinutes = toSave.DurationMinutes;
            existing.DistanceMiles = toSave.DistanceMiles;
            existing.ClimbFeet = toSave.ClimbFeet;
            existing.DescentFeet = toSave.DescentFeet;
            existing.Calories = toSave.Calories;
            existing.Note = toSave.Note;
        }

        await context.SaveChangesAsync();

        DataNotifications.PublishDataNotification(
            "Workout Item Editor",
            DataNotificationContentType.Workout,
            isNew ? DataNotificationUpdateType.New : DataNotificationUpdateType.Update,
            [toSave.ContentId]);

        await StatusContext.ToastSuccess($"Saved Workout ({toSave.WorkoutType} on {toSave.WorkoutOn:d})");

        await LoadData(toSave);

        if (closeAfterSave)
        {
            await ThreadSwitcher.ResumeForegroundAsync();
            RequestContentEditorWindowClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
