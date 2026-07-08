using Godot;

namespace ColdWarWargame.Rendering
{
    public partial class GameCamera : Node3D
    {
        [Export] public float Distance { get; set; } = 18f;
        [Export] public float MinDist { get; set; } = 5f;
        [Export] public float MaxDist { get; set; } = 50f;
        [Export] public Vector3 Target { get; set; } = Vector3.Zero;

        private Camera3D _cam;
        private Vector2 _rotation = new Vector2(0.9f, -0.5f); // pitch, yaw radians

        public Camera3D Cam => _cam;

        public override void _Ready()
        {
            _cam = new Camera3D();
            _cam.Current = true;
            _cam.Position = new Vector3(0, 0, 0);
            AddChild(_cam);
            UpdateCamera();
        }

        void UpdateCamera()
        {
            float cosP = Mathf.Cos(_rotation.X);
            float sinP = Mathf.Sin(_rotation.X);
            float cosY = Mathf.Cos(_rotation.Y);
            float sinY = Mathf.Sin(_rotation.Y);

            Vector3 offset = new Vector3(sinY * cosP, sinP, cosY * cosP) * Distance;
            _cam.Position = Target + offset;
            _cam.LookAt(Target);
        }

        public override void _Input(InputEvent @event)
        {
            // Scroll zoom
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp) { Distance = Mathf.Max(MinDist, Distance * 0.9f); UpdateCamera(); }
                if (mb.ButtonIndex == MouseButton.WheelDown) { Distance = Mathf.Min(MaxDist, Distance * 1.1f); UpdateCamera(); }
            }

            // Right-drag orbit
            if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Right))
            {
                _rotation.Y -= mm.Relative.X * 0.005f;
                _rotation.X = Mathf.Clamp(_rotation.X - mm.Relative.Y * 0.005f, 0.1f, 1.4f);
                UpdateCamera();
            }

            // Middle-drag pan
            if (@event is InputEventMouseMotion mm2 && Input.IsMouseButtonPressed(MouseButton.Middle))
            {
                var right = _cam.GlobalTransform.Basis.X;
                var up = _cam.GlobalTransform.Basis.Y;
                Target += right * mm2.Relative.X * 0.02f * (Distance / 10f);
                Target -= up * mm2.Relative.Y * 0.02f * (Distance / 10f);
                UpdateCamera();
            }
        }

        public override void _Process(double delta)
        {
            float s = 4f * (float)delta * (Distance / 10f);
            if (Input.IsKeyPressed(Key.W)) Target -= new Vector3(0, 0, s);
            if (Input.IsKeyPressed(Key.S)) Target += new Vector3(0, 0, s);
            if (Input.IsKeyPressed(Key.A)) Target -= new Vector3(s, 0, 0);
            if (Input.IsKeyPressed(Key.D)) Target += new Vector3(s, 0, 0);
            if (Input.IsKeyPressed(Key.Space)) UpdateCamera();
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.D))
                UpdateCamera();
        }
    }
}
