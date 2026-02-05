using Godot;
using System;

public partial class LaserSensor : Node3D
{
	[ExportGroup("Sensor Settings")]
	[Export] public float Distance { get; set; } = 10.0f;
	[Export] public float ScanRate { get; set; } = 0.1f; // 100ms entre leituras
	
	[ExportGroup("Visual Settings")]
	[Export] public bool DebugBeam
	{
		get => debugBeam;
		set
		{
			debugBeam = value;
			if (rayMarker != null)
				rayMarker.Visible = value;
		}
	}
	[Export] public Color CollisionColor { get; set; } = new Color(1, 0, 0);
	[Export] public Color ScanColor { get; set; } = new Color(0, 1, 0);

	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	private bool debugBeam = true;

	// Medição
	private float distanceToTarget = 0.0f;
	private float lastSentDistance = -1f;
	private double scanTimer = 0.0;

	// Tag OPC
	private string tagSensor = "";

	// Componentes Visuais
	private Marker3D rayMarker;
	private MeshInstance3D rayMesh;
	private CylinderMesh cylinderMesh;
	private StandardMaterial3D rayMaterial;

	// Referências
	private Root Main;
	private int currentScene;

	public override void _Ready()
	{
		GD.Print("\n> [LaserSensor.cs] [_Ready()]");
		
		Inicializacao_Visuais();
		Conectar_Root();
	}

	public override void _ExitTree()
	{
		if (Main == null) return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
	}

	private void Inicializacao_Visuais()
	{
		rayMarker = GetNode<Marker3D>("RayMarker");
		rayMesh = GetNode<MeshInstance3D>("RayMarker/MeshInstance3D");

		cylinderMesh = rayMesh.Mesh.Duplicate() as CylinderMesh;
		rayMesh.Mesh = cylinderMesh;
		
		rayMaterial = cylinderMesh.Material.Duplicate() as StandardMaterial3D;
		cylinderMesh.Material = rayMaterial;

		// Inicializa visual
		rayMarker.Visible = debugBeam;
		rayMaterial.AlbedoColor = ScanColor;
		cylinderMesh.Height = Distance;
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[LaserSensor] {Name}: Root não encontrado!");
			return;
		}

		currentScene = Main.currentScene;

		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
	}

	private void OnSimulationStarted()
	{
		GD.Print($"\n> [LaserSensor.cs] {Name}: [OnSimulationStarted()]");

		// Carrega tag configurada
		tagSensor = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";

		if (string.IsNullOrEmpty(tagSensor))
		{
			GD.PrintErr($"[LaserSensor] {Name}: Tag OPC não configurada!");
		}

		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");

		if (isCommsConnected)
		{
			GD.Print($"[LaserSensor] {Name}: OPC conectado → {tagSensor}");
		}
		else
		{
			GD.PrintErr($"[LaserSensor] {Name}: OPC não conectado - sensor não enviará dados!");
		}

		// Reset de estado
		running = true;
		distanceToTarget = Distance;
		lastSentDistance = -1f;
		scanTimer = 0.0;
	}

	private void OnSimulationEnded()
	{
		running = false;
		distanceToTarget = Distance;
		lastSentDistance = -1f;
		scanTimer = 0.0;

		// Reset visual
		cylinderMesh.Height = Distance;
		rayMaterial.AlbedoColor = ScanColor;
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!running) return;

		Atualizar_Raycast();

		scanTimer += delta;
		if (scanTimer >= ScanRate)
		{
			scanTimer = 0.0;
			Enviar_Distancia_Opc();
		}
	}

	private void Atualizar_Raycast()
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			rayMarker.GlobalPosition,
			rayMarker.GlobalPosition + GlobalTransform.Basis.Z * Distance
		);
		query.CollisionMask = 8;
		
		var result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			// Objeto detectado
			var hitPosition = (Vector3)result["position"];
			distanceToTarget = rayMarker.GlobalPosition.DistanceTo(hitPosition);
			
			cylinderMesh.Height = distanceToTarget;
			rayMaterial.AlbedoColor = CollisionColor;
		}
		else
		{
			// Sem detecção
			distanceToTarget = Distance;
			
			cylinderMesh.Height = Distance;
			rayMaterial.AlbedoColor = ScanColor;
		}

		// Atualiza posição do mesh
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	private void Enviar_Distancia_Opc()
	{
		if (!isCommsConnected || string.IsNullOrEmpty(tagSensor))
			return;

		// Só envia se distância mudou significativamente (5mm)
		if (Mathf.Abs(distanceToTarget - lastSentDistance) > 0.005f)
		{
			lastSentDistance = distanceToTarget;

			try
			{
				Main.Write(tagSensor, distanceToTarget);

				if (Main.DebugOpcEvents)
				{
					GD.Print($"[LaserSensor] {Name}: Distância = {distanceToTarget:F3}m");
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"[LaserSensor] {Name}: Erro ao escrever {tagSensor}:");
				GD.PrintErr(e.Message);
			}
		}
	}
}