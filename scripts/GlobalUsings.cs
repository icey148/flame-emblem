// 全局别名明确指定游戏脚本中的 Timer 使用 Godot.Timer。
// 项目启用了 ImplicitUsings，.NET 会同时引入 System.Threading.Timer；
// 通过这个别名可以避免 Godot.Timer 与 System.Threading.Timer 的类型歧义。
global using Timer = Godot.Timer;
