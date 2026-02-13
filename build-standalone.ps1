# Build Standalone (Self-Contained) Release
# Users can run without .NET installed

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish-standalone
