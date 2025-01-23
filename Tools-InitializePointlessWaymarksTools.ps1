# Prompt the user for a username
$userName = Read-Host "Enter your username (leave blank if none)"

# Check if the username is provided
if ($userName) {
    # Run the fossil clone command with the username
    fossil clone https://$userName@chiselapp.com/user/cmiles/repository/pointless-waymarks-tools pointless-waymarks-tools.fossil
} else {
    # Run the fossil clone command without the username
    fossil clone https://chiselapp.com/user/cmiles/repository/pointless-waymarks-tools pointless-waymarks-tools.fossil
}

# Define the target directory
$targetDirectory = "PointlessWaymarksTools"

# Check if the target directory exists
if (Test-Path -Path $targetDirectory) {
    Write-Warning "The target directory '$targetDirectory' already exists. Exiting the program."
    exit
} else {
    # Create the target directory
    New-Item -ItemType Directory -Path $targetDirectory
}

fossil open pointless-waymarks-tools.fossil --nested --workdir .\PointlessWaymarksTools