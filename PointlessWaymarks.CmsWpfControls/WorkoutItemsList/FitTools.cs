using System.IO;
using Dynastream.Fit;
using Microsoft.EntityFrameworkCore;
using PointlessWaymarks.CmsData;
using PointlessWaymarks.CmsData.Database;
using PointlessWaymarks.CmsData.Database.Models;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon;
using Serilog;

namespace PointlessWaymarks.CmsWpfControls.WorkoutItemsList;

public static class FitTools
{
    public static WorkoutItem? WorkoutItemFromFitFile(FileInfo fitFile)
    {
        try
        {
            if (!fitFile.Exists) return null;

            using var stream = fitFile.OpenRead();
            var decode = new Decode();
            var broadcaster = new MesgBroadcaster();

            SessionMesg? session = null;
            broadcaster.SessionMesgEvent += (_, e) => session = new SessionMesg(e.mesg);

            decode.MesgEvent += broadcaster.OnMesg;
            decode.MesgDefinitionEvent += broadcaster.OnMesgDefinition;

            if (!decode.IsFIT(stream)) return null;
            stream.Position = 0;
            if (!decode.CheckIntegrity(stream)) return null;
            stream.Position = 0;

            decode.Read(stream);
            if (session == null) return null;

            var startTime = session.GetStartTime()?.GetDateTime().ToLocalTime()
                            ?? session.GetTimestamp()?.GetDateTime().ToLocalTime()
                            ?? System.DateTime.Now;

            var sport = session.GetSport();
            var sportName = sport switch
            {
                Sport.Running => "Run",
                Sport.Cycling => "Bike",
                Sport.Hiking => "Hike",
                Sport.Walking => "Walk",
                Sport.Swimming => "Swim",
                Sport.FitnessEquipment or Sport.Training => "Strength",
                var s when s != Sport.Generic && s != Sport.Invalid => s.ToString() ?? "Workout",
                _ => "Workout"
            };

            var durationSec = session.GetTotalTimerTime() ?? session.GetTotalElapsedTime() ?? 0f;
            var durationMin = Math.Max(1, (int)Math.Round(durationSec / 60.0));

            var distanceMeters = session.GetTotalDistance();
            double? distanceMiles = distanceMeters.HasValue && distanceMeters.Value > 0
                ? Math.Round(((double)distanceMeters.Value).MetersToMiles(), 2)
                : null;

            var ascentMeters = session.GetTotalAscent();
            var climbFeet = ascentMeters.HasValue
                ? (int)Math.Round(((double)ascentMeters.Value).MetersToFeet())
                : 0;

            var descentMeters = session.GetTotalDescent();
            var descentFeet = descentMeters.HasValue
                ? (int)Math.Round(((double)descentMeters.Value).MetersToFeet())
                : 0;

            int? calories = session.GetTotalCalories() is { } c and > 0 ? (int)c : null;

            return new WorkoutItem
            {
                ContentId = Guid.NewGuid(),
                WorkoutOn = startTime,
                WorkoutType = sportName,
                WorkoutBy = UserSettingsSingleton.CurrentSettings().DefaultCreatedBy.TrimNullToEmpty(),
                DurationMinutes = durationMin,
                DistanceMiles = distanceMiles,
                ClimbFeet = climbFeet,
                DescentFeet = descentFeet,
                Calories = calories,
                Note = $"Imported from {fitFile.Name}"
            };
        }
        catch (Exception ex)
        {
            Log.ForContext(nameof(fitFile), fitFile.FullName, false)
                .Error(ex, "Error reading .fit file {File}", fitFile.FullName);
            return null;
        }
    }

    public static async Task<(bool hasError, string generationNote)> SaveWorkoutItem(WorkoutItem toSave)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(toSave.WorkoutType))
            return (true, "Workout Type cannot be blank.");

        if (toSave.DurationMinutes <= 0)
            return (true, "Duration must be greater than 0.");

        try
        {
            if (toSave.ContentId == Guid.Empty)
                toSave.ContentId = Guid.NewGuid();

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
                    return (true, $"Could not find existing Workout Item with Id {toSave.Id}");

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
                "Workout Item",
                DataNotificationContentType.Workout,
                isNew ? DataNotificationUpdateType.New : DataNotificationUpdateType.Update,
                [toSave.ContentId]);

            return (false, string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving Workout Item {ContentId}", toSave.ContentId);
            return (true, ex.Message);
        }
    }
}
