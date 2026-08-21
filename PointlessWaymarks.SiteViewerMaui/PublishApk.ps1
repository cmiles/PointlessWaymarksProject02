#Simplest possible publish for sideloading - apk will end up in \bin\Release\net10.0-android\publish
#Currently using the signed version personally
dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk