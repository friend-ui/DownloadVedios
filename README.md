# 使用VS工具开发
解压yt-dlp.zip到项目根目录

# 标准发布（推荐）
dotnet publish -c Release

# 如果需要更小的文件体积（裁剪模式，可能影响部分功能）
dotnet publish -c Release -p:PublishTrimmed=true