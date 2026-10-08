namespace DesktopWeather.Models;

internal static class WeatherModeInfo
{
    internal static string Name(WeatherMode mode) => mode switch
    {
        WeatherMode.Petals => "樱花", WeatherMode.Fireflies => "萤火虫", WeatherMode.Rain => "夏雨",
        WeatherMode.Leaves => "落叶", _ => "雪花"
    };
    internal static string StartLabel(WeatherMode mode) => mode switch
    {
        WeatherMode.Petals => "开始飘花", WeatherMode.Fireflies => "开启萤火虫", WeatherMode.Rain => "开始下雨",
        WeatherMode.Leaves => "开始落叶", _ => "开始下雪"
    };
    internal static string RunningLabel(WeatherMode mode) => mode switch
    {
        WeatherMode.Petals => "樱花飘落", WeatherMode.Fireflies => "萤火闪烁", WeatherMode.Rain => "夏雨纷纷",
        WeatherMode.Leaves => "落叶中", _ => "下雪中"
    };
    internal static string Title(WeatherMode mode) => mode switch
    {
        WeatherMode.Petals => "让春天，轻轻绽放。", WeatherMode.Fireflies => "让夏夜，亮起微光。",
        WeatherMode.Rain => "让夏雨，带来清凉。", WeatherMode.Leaves => "让秋天，随风而来。",
        _ => "让雪，慢慢落下。"
    };
    internal static string Subtitle(WeatherMode mode) => mode switch
    {
        WeatherMode.Petals => "一场粉白花瓣的春日漫游", WeatherMode.Fireflies => "点点萤火，缓缓漂浮与闪烁",
        WeatherMode.Rain => "细雨随风落下，桌面添一份清凉", WeatherMode.Leaves => "一阵温柔的秋日微风",
        _ => "近景轻柔，远景清晰的一场小雪"
    };
}
