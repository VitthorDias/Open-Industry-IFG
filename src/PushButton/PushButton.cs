using Godot;
using System;
using System.Threading.Tasks;

public partial class PushButton : Node3D
{
	[Export]
	public String Text
	{
		get
		{
			return text;
		}
		set
		{
			text = value;
			Atualizar_Texto();
		}
	}

	[Export]
	public bool Toggle
	{
		get
		{
			return toggle;
		}
		set
		{
			toggle = value;

			if (!toggle)
				pushbutton = false;
		}
	}

	[Export]
	public bool Lamp
	{
		get
		{
			return lamp;
		}
		set
		{
			lamp = value;
			Atualizar_Estado_Lamp(lamp);
		}
	}

	[Export]
	public Color ButtonColor
	{
		get
		{
			return buttonColor;
		}
		set
		{
			buttonColor = value;
			Atualizar_Cor_Botao(buttonColor);
		}
	}

	// Estado
	private string text = "stop";
	private bool toggle = false;
	private bool pushbutton = false;
	private bool lamp = false;
	private Color buttonColor = new Color("#e73d30");
	
	private bool running = false;
	private bool isCommsConnected = false;
	private bool lastSentState = false;

	// TagOpc
	private string tagPushButton;

	// Componentes
	private MeshInstance3D textMeshInstance;
	private TextMesh textMesh;
	private MeshInstance3D buttonMesh;
	private StandardMaterial3D buttonMaterial;
	
	// Constantes
	private const float buttonPressedZPos = -0.04f;

	// Referências
	public Root Main { get; set; }

	public override void _Ready()
	{
		Inicializacao_Componentes();
		Conectar_Root();
		Inicializacao_Visuais();
	}

	public override void _ExitTree()
	{
		if (Main == null)
			return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
	}

	private void Inicializacao_Componentes()
	{
		// Assign 3D text
		textMeshInstance = GetNode<MeshInstance3D>("TextMesh");
		textMesh = textMeshInstance.Mesh.Duplicate() as TextMesh;
		textMeshInstance.Mesh = textMesh;

		// Assign button
		buttonMesh = GetNode<MeshInstance3D>("Meshes/Button");
		buttonMesh.Mesh = buttonMesh.Mesh.Duplicate() as Mesh;
		buttonMaterial = buttonMesh.Mesh.SurfaceGetMaterial(0).Duplicate() as StandardMaterial3D;
		buttonMesh.Mesh.SurfaceSetMaterial(0, buttonMaterial);
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[PushButton] {Name}: Root não encontrado!");
			return;
		}
		
		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
	}

	private void Inicializacao_Visuais()
	{
		Atualizar_Texto();
		Atualizar_Cor_Botao(ButtonColor);
		Atualizar_Estado_Lamp(Lamp);
	}

	private void OnSimulationStarted()
	{
		// Carrega tag configurada
		tagPushButton = SceneComponents.GetComponentByKey(Name, Main.currentScene)?.Tag ?? "";
		
		if (string.IsNullOrEmpty(tagPushButton))
		{
			GD.PrintErr($"[PushButton] {Name}: Tag OPC não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[PushButton] {Name}: OPC conectado → {tagPushButton}");
		}
		
		// Reset de estado
		running = true;
		pushbutton = false;
		lastSentState = false;
	}

	private void OnSimulationEnded()
	{
		running = false;
		pushbutton = false;
		lastSentState = false;
		Lamp = false;

		buttonMesh.Position = Vector3.Zero;
		Atualizar_Estado_Lamp(false);
	}

	private void _on_static_body_3d_input_event(Node camera, InputEvent inputEvent, Vector3 position, Vector3 normal, int shapeIdx)
	{
		if (!running)
			return;

		if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (Toggle)
			{
				if (mouseEvent.Pressed)
				{
					HandleToggleClick();
				}
			}
			else
			{
				if (mouseEvent.Pressed)
				{
					HandleButtonPress(); // Mouse DOWN
				}
				else
				{
					HandleButtonRelease(); // Mouse UP
				}
			}
		}
	}

	// Modo Toggle: Alterna estado ao clicar
	private void HandleToggleClick()
	{
		pushbutton = !pushbutton;
		
		// Atualiza visual
		Atualizar_Visual_Botao();
		Atualizar_Estado_Lamp(pushbutton);
		
		// Envia para OPC se estado mudou
		if (isCommsConnected && !string.IsNullOrEmpty(tagPushButton) && pushbutton != lastSentState)
		{
			lastSentState = pushbutton;
			WriteTag();
		}
		
		if (Main.DebugOpcEvents)
		{
			GD.Print($"[PushButton] {Name}: Toggle → {pushbutton}");
		}
	}

	// Modo Momentary: Ativa quando pressiona
	private void HandleButtonPress()
	{
		pushbutton = true;
		
		// Atualiza visual
		Atualizar_Visual_Botao();
		Atualizar_Estado_Lamp(true);
		
		// Envia TRUE para OPC
		if (isCommsConnected && !string.IsNullOrEmpty(tagPushButton))
		{
			lastSentState = true;
			WriteTag();
		}
		
		if (Main.DebugOpcEvents)
		{
			GD.Print($"[PushButton] {Name}: Pressionado → True");
		}
	}

	// Modo Momentary: Desativa quando solta
	private void HandleButtonRelease()
	{
		pushbutton = false;
		
		// Atualiza visual
		Atualizar_Visual_Botao();
		Atualizar_Estado_Lamp(false);
		
		// Envia FALSE para OPC
		if (isCommsConnected && !string.IsNullOrEmpty(tagPushButton))
		{
			lastSentState = false;
			WriteTag();
		}
		
		if (Main.DebugOpcEvents)
		{
			GD.Print($"[PushButton] {Name}: Solto → False");
		}
	}

	private void Atualizar_Visual_Botao()
	{
		if (pushbutton)
		{
			buttonMesh.Position = new Vector3(0, 0, buttonPressedZPos);
		}
		else
		{
			buttonMesh.Position = Vector3.Zero;
		}
	}

	private void Atualizar_Texto()
	{
		if (textMesh != null)
		{
			textMesh.Text = text;
		}
	}
	private void Atualizar_Estado_Lamp(bool active)
	{
		if (buttonMaterial == null) 
			return;
		
		var textMaterial = new StandardMaterial3D();
		
		if (active)
		{
			// Acende LED
			buttonMaterial.EmissionEnergyMultiplier = 1.0f;
			
			// Cor do texto baseada no tipo de botão
			if (text == "Iniciar" || text == "Ligar")
			{
				textMaterial.AlbedoColor = new Color(0.2392f, 0.9059f, 0.1882f); // Verde
			}
			else if (text == "Pausar" || text == "Desligar")
			{
				textMaterial.AlbedoColor = new Color(0.9059f, 0.2392f, 0.1882f); // Vermelho
			}
			else
			{
				textMaterial.AlbedoColor = new Color(1.0f, 0.6588f, 0.0f); // Laranja
			}
		}
		else
		{
			// Apaga LED
			buttonMaterial.EmissionEnergyMultiplier = 0.0f;
			textMaterial.AlbedoColor = new Color(1.0f, 0.6588f, 0.0f); // Laranja padrão
		}
		
		if (textMesh != null)
		{
			textMesh.Material = textMaterial;
		}
	}

	private void Atualizar_Cor_Botao(Color newColor)
	{
		if (buttonMaterial != null)
		{
			buttonMaterial.AlbedoColor = newColor;
			buttonMaterial.Emission = newColor;
		}
	}

	private void WriteTag()
	{
		try
		{
			Main.Write(tagPushButton, pushbutton);
			
			if (Main.DebugOpcEvents)
			{
				GD.Print($"[PushButton] {Name}: {pushbutton} → {tagPushButton}");
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[PushButton] {Name}: Erro ao escrever {tagPushButton}:");
			GD.PrintErr(e.Message);
		}
	}
}