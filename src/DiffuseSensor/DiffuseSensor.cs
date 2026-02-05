using Godot;
using System;
using System.Threading.Tasks;

public partial class DiffuseSensor : Node3D
{
	[Export]
	float distance = 6.0f;
	[Export]
	bool DebugBeam
	{
		get
		{
			return debugBeam;
		}
		set
		{
			debugBeam = value;
			if (rayMarker != null)
				rayMarker.Visible = value;
		}
	}
	[Export]
	public Color collisionColor {get; set;} = new Color(1, 0, 0);

	[Export]
	public Color scanColor {get; set;} = new Color(0, 1, 0);
	
	[Export]
	float scanRate {get; set;} = 0.05f; // Verifica colisão a cada 50ms

	private bool debugBeam = true;
	private bool running = false;	
	private bool isCommsConnected;

	private CollisionObject3D clickableArea;
	private float savedRotationY = 0f;
	private float originalRotationY = 0f; // Rotação original da cena Godot
	private int currentScene = 0;

	private bool blocked = false;
	private bool lastSentState = false; // Ultimo estado enviado ao OPC

	private string tagDiffuseSensor = "";

	private Marker3D rayMarker;
	private MeshInstance3D rayMesh;
	private CylinderMesh cylinderMesh;
	private StandardMaterial3D rayMaterial;

	private double scan_interval = 0;

	private Root Main;

	private System.Diagnostics.Stopwatch writeLatencyWatch = new System.Diagnostics.Stopwatch();
    private bool measureNextRead = false;
	
	public override void _Ready()
	{
		originalRotationY = RotationDegrees.Y;

		Inicializacao_Visuais();
		Criar_Area_Clicavel();
		Conectar_Root();

		LoadSavedRotation();
	}

	public override void _ExitTree()
	{
		if (Main == null) return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
	}

	private void Inicializacao_Visuais()
	{
		GD.Print("\n> [DiffuseSensor.cs] [_Ready()]");
		rayMarker = GetNode<Marker3D>("RayMarker");
		rayMesh = GetNode<MeshInstance3D>("RayMarker/MeshInstance3D");

		cylinderMesh = rayMesh.Mesh.Duplicate() as CylinderMesh;
		rayMesh.Mesh = cylinderMesh;
		rayMaterial = cylinderMesh.Material.Duplicate() as StandardMaterial3D;
		cylinderMesh.Material = rayMaterial;

		// Inicializa visual
		rayMarker.Visible = debugBeam;
		rayMaterial.AlbedoColor = scanColor;
		cylinderMesh.Height = distance;
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	private void Criar_Area_Clicavel()
	{
		// Cria StaticBody3D para detecção de clique
		var staticBody = new StaticBody3D { Name = "ClickableArea" };
		AddChild(staticBody);
		
		// Collision shape baseado no corpo do sensor (ajuste conforme seu modelo)
		var collisionShape = new CollisionShape3D();
		var boxShape = new BoxShape3D { Size = new Vector3(0.5f, 0.5f, 0.5f) }; // Ajuste o tamanho
		collisionShape.Shape = boxShape;
		staticBody.AddChild(collisionShape);
		
		// Configuração de camadas (layer para objetos clicáveis)
		staticBody.CollisionLayer = 16; // Layer 5 (2^4 = 16)
		staticBody.CollisionMask = 0;
		
		clickableArea = staticBody;
		
		// Adiciona metadata para identificar este sensor
		staticBody.SetMeta("sensor_name", Name);
		staticBody.SetMeta("sensor_instance", this);
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"\n> [DiffuseSensor.cs] {Name}: Root não encontrado!");
			return;
		}

		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
	}

	private void OnSimulationStarted()
	{
		tagDiffuseSensor = SceneComponents.GetComponentByKey(Name, Main.currentScene)?.Tag ?? "";

		if (string.IsNullOrEmpty(tagDiffuseSensor))
		{
			GD.Print($"\n> [DiffuseSensor.cs] {Name}: Tag OPC não configurada.");
		}

		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");

		savedRotationY = RotationDegrees.Y;

		running = true;
		blocked = false;
		lastSentState = false; // Reset do estado
		scan_interval = 0;
	}

	private void OnSimulationEnded()
	{
		running = false;
		blocked = false;
		lastSentState = false;
		
		cylinderMesh.Height = distance;
		rayMaterial.AlbedoColor = scanColor;
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!running) return;
		
		scan_interval += delta;

		if (scan_interval < scanRate) return;

		scan_interval = 0;

		bool detectedBlocked = PerformRaycast();

		if (detectedBlocked != blocked)
		{
			blocked = detectedBlocked;
			UpdateVisuals();

			if (isCommsConnected && !string.IsNullOrEmpty(tagDiffuseSensor) && blocked != lastSentState)
			{
				lastSentState = blocked;
				WriteTag();
			}
		}
	}

	private bool PerformRaycast()
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			rayMarker.GlobalPosition,
			rayMarker.GlobalPosition + GlobalTransform.Basis.Z * distance
		);
		query.CollisionMask = 8;
		
		var result = spaceState.IntersectRay(query);
		
		return result.Count > 0;
	}

	private void UpdateVisuals()
	{
		if (blocked)
		{
			// Objeto detectado
			cylinderMesh.Height = distance * 0.5f;
			rayMaterial.AlbedoColor = collisionColor;
		}
		else
		{
			// Sem detecção
			cylinderMesh.Height = distance;
			rayMaterial.AlbedoColor = scanColor;
		}
		
		rayMesh.Position = new Vector3(0, 0, cylinderMesh.Height * 0.5f);
	}

	private void WriteTag()
	{
		try
		{
			Main.Write(tagDiffuseSensor, blocked);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[DiffuseSensor] {Name}: Erro ao escrever {tagDiffuseSensor}:");
			GD.PrintErr(e.Message);
		}
	}

	private void LoadSavedRotation()
	{
		var main = GetTree().CurrentScene as Root;
		if (main == null)
			return;
		
		currentScene = main.currentScene;
		
		// Tenta carregar rotação salva
		float? savedRotation = SceneConfigManager.LoadSensorRotation(Name, currentScene);
		
		if (savedRotation.HasValue)
		{
			// Se encontrar no JSON: Aplica rotação salva
			RotationDegrees = new Vector3(RotationDegrees.X, savedRotation.Value, RotationDegrees.Z);
			GD.Print($"[DiffuseSensor] {Name}: Rotação carregada = {savedRotation.Value:F1}°");
		}
		else
		{
			// Se NÃO encontrar no JSON: Mantém rotação original da cena
			GD.Print($"[DiffuseSensor] {Name}: Usando rotação padrão = {originalRotationY:F1}°");
		}
	}

	public void RestoreOriginalRotation()
	{
		// Não permite durante simulação
		if (running) 
			return;
		
		// Remover configurações do arquivo JSON
		SceneConfigManager.RemoveSensorRotation(Name, currentScene);
		
		// Volta para rotação original da cena
		RotationDegrees = new Vector3(RotationDegrees.X, originalRotationY, RotationDegrees.Z);
		
		GD.Print($"[DiffuseSensor] {Name}: Restaurado para rotação original = {originalRotationY:F1}°");
	}

	// Método público para aplicar rotação (chamado pelo UI)
	public void SetRotationY(float degrees, bool save = false)
	{
		// Não permite rotação durante simulação
		if (running) 
			return;
		
		RotationDegrees = new Vector3(RotationDegrees.X, degrees, RotationDegrees.Z);

		if (save)
		{
			SceneConfigManager.SaveSensorRotation(Name, degrees, currentScene);
		}
	}

	// Retorna rotação atual
	public float GetRotationY()
	{
		return RotationDegrees.Y;
	}

	public void ApplyOriginalRotation()
	{
		RotationDegrees = new Vector3(RotationDegrees.X, originalRotationY, RotationDegrees.Z);
		GD.Print($"[DiffuseSensor] {Name}: Rotação aplicada = {originalRotationY:F1}°");
	}
}