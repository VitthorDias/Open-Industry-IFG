using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Root : Node3D
{
	public int currentScene;
	[Export] int CurrentScene
	{
		get
		{
			return currentScene;
		}
		set
		{
			currentScene = value;
		}
	}

	[Signal]
	public delegate void SimulationStartedEventHandler();
	[Signal]
	public delegate void SimulationSetPausedEventHandler(bool paused);
	[Signal]
	public delegate void SimulationEndedEventHandler();

	// Adicionado
	public delegate void OpcDataChangedHandler(string tagName, object value);
    public event OpcDataChangedHandler OpcDataChanged;
	// ---

	public void NotifyOpcDataChanged(string tagName, object value)
    {
		if (DebugOpcEvents)
		{
			GD.Print($"[Root] OPC Update: {tagName} = {value}");
		}
      	
		OpcDataChanged?.Invoke(tagName, value);
    }

	[Export] public bool DebugOpcEvents = false;

	private bool _start = false;
	public bool Start
	{
		get
		{
			return _start;
		}
		set
		{
			_start = value;

			if (_start)
			{
				PhysicsServer3D.SetActive(true);
				EmitSignal(SignalName.SimulationStarted);
			}
			else
			{
				PhysicsServer3D.SetActive(false);
				EmitSignal(SignalName.SimulationEnded);
			}
		}
	}

	public bool paused = false;

	private RichTextLabel textoCommsState;

	private readonly List<Vector3> positions = new();
	private readonly List<Vector3> rotations = new();

	private SensorRotationUI sensorRotationUI;
	private Camera3D playerCamera;

	private void SavePositions()
	{
		GD.Print("\n> [Root.cs] [SavePositions()]");
		foreach (Node3D node in GetNode<Node3D>("Building").GetChildren())
		{
			positions.Add(node.Position);
			rotations.Add(node.Rotation);
		}
	}

	private void ResetPositions()
	{
		GD.Print("\n> [Root.cs] [ResetPositions()]");
		int i = 0;
		foreach (Node3D node in GetNode<Node3D>("Building").GetChildren())
		{
			node.Position = positions[i];
			node.Rotation = rotations[i];
			i++;
		}
	}

	public override void _Ready()
	{
		GD.Print("\n> [Root.cs] [_Ready()]");

		Inicializacao_UI();
		ConectarEventosGlobais();

		Criar_Sensor_Rotation_UI();
		playerCamera = FindCameraInScene();

		if (playerCamera != null)
		{
			GD.Print($"[Root] Câmera encontrada: {playerCamera.GetPath()}");
		}
		else
		{
			GD.PrintErr("[Root] Nenhuma câmera ativa encontrada! Clique em sensores não funcionará.");
		}

		Timer timer = new Timer()
		{
			WaitTime = 1.0,
			OneShot = false,
			Autostart = true
		};
		AddChild(timer);
		timer.Timeout += DefinirTextStatusConexao;
	}

	private Camera3D FindCameraInScene()
	{
		return FindCameraRecursive(GetTree().Root);
	}

	private Camera3D FindCameraRecursive(Node node)
	{
		// Busca Camera3D que esteja ativa (Current = true)
		if (node is Camera3D camera && camera.Current)
		{
			return camera;
		}
		
		foreach (Node child in node.GetChildren())
		{
			var result = FindCameraRecursive(child);
			if (result != null)
				return result;
		}
		
		return null;
	}

	private void Criar_Sensor_Rotation_UI()
	{
		sensorRotationUI = new SensorRotationUI();
		
		// Adiciona como filho de um CanvasLayer para ficar acima de tudo
		var canvasLayer = new CanvasLayer { Name = "SensorUILayer" };
		AddChild(canvasLayer);
		canvasLayer.AddChild(sensorRotationUI);
	}

	private void Inicializacao_UI()
	{
		GetNode<CanvasItem>("CommsConfigMenu").Visible = false;
		textoCommsState = GetNode<RichTextLabel>("Simulation_Control/TextoCommsState");
		textoCommsState.Visible = true;
	}

	private void ConectarEventosGlobais()
	{
		var simulationEvents = GetNodeOrNull("/root/GlobalVariables");
		if (simulationEvents != null)
		{
			simulationEvents.Connect("simulation_started", new Callable(this, nameof(OnSimulationStarted)));
			simulationEvents.Connect("simulation_set_paused", new Callable(this, nameof(OnSimulationSetPaused)));
			simulationEvents.Connect("simulation_ended", new Callable(this, nameof(OnSimulationEnded)));
		} 
		else
		{
			GD.PrintErr("[Root] GlobalVariables não encontrado!");
		}
	}

	public enum DataType
	{
		Bool,
		Int,
		Float
	}

	public async Task<float> ReadFloat(string tagName)
	{
		try
		{
			var result = CommsConfig.ReadOpcItem(tagName);
			if (result == null)
			{
				GD.PrintErr($"[Root] ReadFloat: Tag {tagName} retornou null");
				return 0f;
			}
			return Convert.ToSingle(result);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Root] Erro ao ler float de {tagName}:");
			GD.PrintErr(e.Message);
			return 0f;
		}
	}

	public async Task<bool> ReadBool(string tagName)
	{
		try
		{
			var result = CommsConfig.ReadOpcItem(tagName);
			if (result == null)
			{
				GD.PrintErr($"[Root] ReadBool: Tag {tagName} retornou null");
				return false;
			}
			return Convert.ToBoolean(result);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Root] Erro ao ler bool de {tagName}:");
			GD.PrintErr(e.Message);
			return false;
		}
	}

	public void Write(String tagName, bool value)
{
	try
	{
		CommsConfig.WriteOpcItem(tagName, value);
	}
	catch (Exception e)
	{
		GD.PrintErr($"[Root] Erro ao escrever {tagName}: {e.Message}");
	}
}

public void Write(String tagName, float value)
{
	try
	{
		CommsConfig.WriteOpcItem(tagName, value);
	}
	catch (Exception e)
	{
		GD.PrintErr($"[Root] Erro ao escrever {tagName}: {e.Message}");
	}
}

	private static void PrintError(string error)
	{
		GD.PrintErr(error);
	}

	void OnSimulationStarted()
	{
		GD.Print("\n> [Root.cs] [OnSimulationStarted()] - Simulação iniciada");

		sensorRotationUI?.Hide();
		
		var commsConfig = GetNode<CommsConfig>("CommsConfigMenu");
		if (commsConfig != null)
		{
			commsConfig.AtualizarSubscription();
		}
		else
		{
			GD.PrintErr("CommsConfig não encontrado! Verifique o caminho do nó.");
		}
		
		Start = true;
	}

	void OnSimulationSetPaused(bool _paused)
	{
		paused = _paused;

		if (paused)
		{
			ProcessMode = ProcessModeEnum.Disabled;
			GD.Print("\n> [Root.cs] [OnSimulationSetPaused()] - Simulação pausada");
		}
		else
		{
			ProcessMode = ProcessModeEnum.Inherit;
			GD.Print("\n> [Root.cs] [OnSimulationSetPaused()] - Simulação retomada");
		}

		EmitSignal(SignalName.SimulationSetPaused, paused);
	}

	void OnSimulationEnded()
	{
		GD.Print("\n> [Root.cs] [OnSimulationEnded()] - Simulação finalizada");
		Start = false;
	}

	void DefinirTextStatusConexao()
	{
		// GD.Print("\n> [Root.cs] [DefinirTextStatusConexao()]");

		var globalVariables = GetNodeOrNull("/root/GlobalVariables");

		if (globalVariables == null)
		{
			// RGB: (102, 255, 102) Verde Claro
			textoCommsState.Text = "Comunicação OPC DA: Erro - Variáveis não encontrado";
			textoCommsState.AddThemeColorOverride("default_color", new Color(1.0f, 0.4f, 0.4f, 1.0f));
			return;
		}

		bool IsConnected = (bool)globalVariables.Get("opc_da_connected");

		if (IsConnected)
		{
			textoCommsState.Text = "Comunicação OPC DA: Conectado";
			textoCommsState.AddThemeColorOverride("default_color", new Color(0.4f, 1.0f, 0.4f, 1.0f));
		}
		else
		{
			textoCommsState.Text = "Comunicação OPC DA: Desconectado";
			textoCommsState.AddThemeColorOverride("default_color", new Color(1.0f, 0.4f, 0.4f, 1.0f));
		}
	}

	private Node3D building;

	public override void _Process(double delta)
	{
		//selectedNodes = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
	}

	/// Teste manual de latência OPC (F5 durante a simulação)
	public override void _Input(InputEvent @event)
	{

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			// Só permite clicar se simulação estiver parada
			if (!Start && playerCamera != null)
			{
				DetectarCliqueSensor(mouseEvent.Position);
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.F5)
		{
			TestOpcLatency();
		}
	}

	private void DetectarCliqueSensor(Vector2 mousePosition)
	{
		// Raycast da câmera do player
		var from = playerCamera.ProjectRayOrigin(mousePosition);
		var to = from + playerCamera.ProjectRayNormal(mousePosition) * 1000;
		
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollisionMask = 16; // Layer 5 (sensores clicáveis)
		
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			var collider = result["collider"].As<CollisionObject3D>();
			
			// Verifica se tem metadata de sensor
			if (collider.HasMeta("sensor_instance"))
			{
				var sensor = collider.GetMeta("sensor_instance").As<DiffuseSensor>();
				
				if (sensor != null)
				{
					GD.Print($"[Root] Sensor clicado: {sensor.Name}");
					
					if (sensorRotationUI != null)
					{
						// Aguarda 1 frame se necessário
						CallDeferred(nameof(ShowSensorUIDeferred), sensor);
					}
				}
			}
		}
	}

	private void ShowSensorUIDeferred(DiffuseSensor sensor)
	{
		sensorRotationUI?.ShowForSensor(sensor);
	}

	// Restaurar Configurações do cenário
	public void RestoreSceneConfigs()
	{
		GD.Print("\n> [Root.cs] [RestoreSceneConfigs()] - Restaurando configurações da cena");
		
		// Restaura todas as configurações da cena (exceto OPC)
		SceneConfigManager.RestoreAllConfigs(currentScene);

		AplicarRotacoesOriginaisSensores();
		
		GD.Print($"[Root] Configurações da cena {currentScene} restauradas para padrão.");
	}

	private void AplicarRotacoesOriginaisSensores()
	{
		// Busca TODOS os DiffuseSensor na cena
		var sensores = GetTree().GetNodesInGroup("diffuse_sensors");
		
		if (sensores.Count == 0)
		{
			// Se não houver grupo, busca manualmente na árvore
			sensores = BuscarSensoresNaCena(this);
		}
		
		int count = 0;
		foreach (var node in sensores)
		{
			if (node is DiffuseSensor sensor)
			{
				sensor.ApplyOriginalRotation();
				count++;
			}
		}
		
		GD.Print($"[Root] {count} sensores restaurados para rotação original.");
	}

	// Busca recursiva de sensores
	private Godot.Collections.Array<Node> BuscarSensoresNaCena(Node root)
	{
		var sensores = new Godot.Collections.Array<Node>();
		
		foreach (Node child in root.GetChildren())
		{
			if (child is DiffuseSensor sensor)
			{
				sensores.Add(sensor);
			}
			
			// Busca recursivamente nos filhos
			var filhosSensores = BuscarSensoresNaCena(child);
			foreach (var s in filhosSensores)
			{
				sensores.Add(s);
			}
		}
		
		return sensores;
	}
	
	private async void TestOpcLatency()
	{
		string testTag = "PLC_GW3.Application.IOs_FACTORY_IO.FIO_I03_"; // Ajuste para uma tag válida
		
		GD.Print("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		GD.Print("TESTE DE LATÊNCIA OPC");
		GD.Print("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		
		var globalVars = GetNodeOrNull("/root/GlobalVariables");
		bool isConnected = globalVars != null && (bool)globalVars.Get("opc_da_connected");
		
		if (!isConnected)
		{
			GD.PrintErr("OPC não conectado. Conecte-se primeiro!");
			return;
		}
		
		try
		{
			// Teste de WRITE
			var swWrite = System.Diagnostics.Stopwatch.StartNew();
			Write(testTag, true);
			swWrite.Stop();
			GD.Print($"Write: {swWrite.Elapsed.TotalMilliseconds:F2}ms");
			
			await Task.Delay(200); // Aguarda 200ms
			
			// Teste de READ
			var swRead = System.Diagnostics.Stopwatch.StartNew();
			var value = await ReadBool(testTag);
			swRead.Stop();
			GD.Print($"📖 Read: {swRead.Elapsed.TotalMilliseconds:F2}ms (valor: {value})");
			
			GD.Print("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
			GD.Print("Teste concluído\n");
		}
		catch (Exception e)
		{
			GD.PrintErr("Erro no teste:");
			GD.PrintErr(e.Message);
		}
	}
}
