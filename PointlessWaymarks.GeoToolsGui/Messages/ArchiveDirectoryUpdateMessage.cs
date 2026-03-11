using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.GeoToolsGui.Messages;

public class ArchiveDirectoryUpdateMessage((object sender, string archiveDirectory) message)
    : ValueChangedMessage<(object sender, string archiveDirectory)>(message);