using Godot;
using System;

// 类名必须是 Node3d，与文件名 Node3d.cs 完全一致
public partial class Node3d : Node 
{
	public override void _Ready()
	{
		GD.Print("Hello, Godot!");
	}
}

