using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Diverter : Node3D
{
	[Export]
	public float divertTime { get; set; } = 0.5f;
	[Export]
	public float divertDistance { get; set; } = 1.0f;

	// Estado
	private bool running = false;
	private bool isCommsConnected = false;

	// Controle de disparo
	private bool fireDivert = false;
	private bool previousFireDivertState = false;
	private bool cycled = false;
	private bool divert = false;

	// TagOpc
	private string tagFcAvanco = "";   // INPUT: Fim de curso avanço
	private string tagFcRecuo = "";    // INPUT: Fim de curso recuo
	private string tagDiverter = "";   // OUTPUT: Comando liga/desliga

	// Estados de funcionamento
	private enum DiverterState
	{
		Recuado,      // Totalmente recuado
		Avancando,    // Em movimento para frente
		Avancado,     // Totalmente avançado
		Recuando      // Em movimento para trás
	}

	private DiverterState currentState = DiverterState.Recuado;

	private double stateTimer = 0.0;

	// Estados anteriores dos FCs
	private bool lastFcAvancoState = false;
	private bool lastFcRecuoState = true;

	// Componentes
	private DiverterAnimator diverterAnimator;

	// Referências
	private Root Main;
	private int currentScene;
	
	public override void _Ready()
	{
		Inicializacao_Componentes();
		Conectar_Root();
	}

	public override void _ExitTree()
	{
		if (Main == null) return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
		Main.OpcDataChanged -= OnOpcDataReceived;
	}

	private void Inicializacao_Componentes()
	{
		diverterAnimator = GetNode<DiverterAnimator>("DiverterAnimator");

		if (diverterAnimator == null)
		{
			GD.PrintErr($"[Diverter] {Name}: DiverterAnimator não encontrado!");
		}
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[Diverter] {Name}: Root não encontrado!");
			return;
		}

		currentScene = Main.currentScene;
		
		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
		Main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		// Busca as 3 tags na ordem: FC-Avanço, FC-Recuo, Comando
		var components = GetAllComponentsByKey(Name, currentScene);
		
		int tagIndex = 0;
		foreach (var component in components)
		{
			if (string.IsNullOrEmpty(component.Tag)) continue;
			
			switch (tagIndex)
			{
				case 0: // Primeira tag = FC-Avanço (INPUT)
					if (component.Type == "input")
						tagFcAvanco = component.Tag;
					break;
				
				case 1: // Segunda tag = FC-Recuo (INPUT)
					if (component.Type == "input")
						tagFcRecuo = component.Tag;
					break;
				
				case 2: // Terceira tag = Comando (OUTPUT)
					if (component.Type == "output")
						tagDiverter = component.Tag;
					break;
			}
			tagIndex++;
		}
		
		// Validação
		if (string.IsNullOrEmpty(tagDiverter))
		{
			GD.PrintErr($"[Diverter] {Name}: Tag de comando (INPUT) não configurada!");
		}
		
		if (string.IsNullOrEmpty(tagFcAvanco))
		{
			GD.PrintErr($"[Diverter] {Name}: Tag FC-Avanço (OUTPUT) não configurada!");
		}
		
		if (string.IsNullOrEmpty(tagFcRecuo))
		{
			GD.PrintErr($"[Diverter] {Name}: Tag FC-Recuo (OUTPUT) não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[Diverter] {Name}: OPC conectado");
			GD.Print($"  INPUT (comando) → {tagDiverter}");
			GD.Print($"  OUTPUT (FC-Avanço) → {tagFcAvanco}");
			GD.Print($"  OUTPUT (FC-Recuo) → {tagFcRecuo}");
		}
		else
		{
			GD.PrintErr($"[Diverter] {Name}: OPC não conectado - diverter não funcionará!");
		}
		
		// Reset de estado
		running = true;
		fireDivert = false;
		previousFireDivertState = false;
		cycled = false;
		divert = false;
		currentState = DiverterState.Recuado;
		stateTimer = 0.0;
		lastFcAvancoState = false;
		lastFcRecuoState = false;
		
		// Estado inicial (recuado)
		WriteTag_Fins_Curso(forceWrite: true);
	}

	private void OnSimulationEnded()
	{
		running = false;
		fireDivert = false;
		previousFireDivertState = false;
		cycled = false;
		divert = false;
		currentState = DiverterState.Recuado;
		stateTimer = 0.0;

		if (diverterAnimator != null)
		{
			diverterAnimator.Disable();
		}
	}

	private void OnOpcDataReceived(string tagName, object value)
	{
		// Recebe sinal de disparo do OPC via subscription
		if (tagName != tagDiverter || value == null) 
			return;
		
		try
		{
			bool newFireDivert = Convert.ToBoolean(value);
			
			// Atualiza estado apenas se mudou
			if (newFireDivert != fireDivert)
			{
				fireDivert = newFireDivert;
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[Diverter] {Name}: Comando = {fireDivert}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[Diverter] {Name}: Erro ao converter valor de {tagName}:");
			GD.PrintErr(e.Message);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!running)
		{
			fireDivert = false;
			return;
		}

		// Atualiza timer de estado
		stateTimer += delta;

		// Máquina de estados para controlar fins de curso
		Atualizar_Maquina_Estados(delta);

		// Detecta Borda de Subida
		if (fireDivert && !previousFireDivertState)
		{
			divert = true;
			cycled = false;
		}

		// Executa animação
		if (divert && !cycled)
		{
			if (diverterAnimator != null)
			{
				diverterAnimator.Fire(divertTime, divertDistance);
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[Diverter] {Name}: Disparado! (Time: {divertTime}s, Distance: {divertDistance})");
				}
			}

			divert = false;
			cycled = true;
			
			// Inicia transição para avanço
			currentState = DiverterState.Avancando;
			stateTimer = 0.0;
		}

		// Atualiza Estado
		previousFireDivertState = fireDivert;
	}

	private void Atualizar_Maquina_Estados(double delta)
	{
		switch (currentState)
		{
			case DiverterState.Recuado:
				// Aguardando comando
				break;
			
			case DiverterState.Avancando:
				// Aguarda tempo de avanço
				if (stateTimer >= divertTime / 2.0) // Metade do tempo = totalmente avançado
				{
					currentState = DiverterState.Avancado;
					stateTimer = 0.0;
					WriteTag_Fins_Curso();
					
					if (Main.DebugOpcEvents)
					{
						GD.Print($"[Diverter] {Name}: Estado → AVANÇADO");
					}
				}
				break;
			
			case DiverterState.Avancado:
				// Aguarda tempo antes de recuar
				if (stateTimer >= divertTime / 2.0) // Metade do tempo avançado
				{
					currentState = DiverterState.Recuando;
					stateTimer = 0.0;
					WriteTag_Fins_Curso();
					
					if (Main.DebugOpcEvents)
					{
						GD.Print($"[Diverter] {Name}: Estado → RECUANDO");
					}
				}
				break;
			
			case DiverterState.Recuando:
				// Aguarda tempo de recuo
				if (stateTimer >= divertTime / 2.0) // Metade do tempo = totalmente recuado
				{
					currentState = DiverterState.Recuado;
					stateTimer = 0.0;
					WriteTag_Fins_Curso();
					
					if (Main.DebugOpcEvents)
					{
						GD.Print($"[Diverter] {Name}: Estado → RECUADO");
					}
				}
				break;
		}
	}

	private void WriteTag_Fins_Curso(bool forceWrite = false)
	{
		if (!isCommsConnected) return;
		
		bool fcAvancoAtivo = false;
		bool fcRecuoAtivo = false;
		
		switch (currentState)
		{
			case DiverterState.Recuado:
				fcAvancoAtivo = false;
				fcRecuoAtivo = true;
				break;
			
			case DiverterState.Avancando:
			case DiverterState.Recuando:
				fcAvancoAtivo = false;
				fcRecuoAtivo = false;
				break;
			
			case DiverterState.Avancado:
				fcAvancoAtivo = true;
				fcRecuoAtivo = false;
				break;
		}
		
		// Envia apenas se mudou ou se esta forçado
		if ((fcAvancoAtivo != lastFcAvancoState || forceWrite) && !string.IsNullOrEmpty(tagFcAvanco))
		{
			Main.Write(tagFcAvanco, fcAvancoAtivo);
			lastFcAvancoState = fcAvancoAtivo;
			
			if (Main.DebugOpcEvents)
			{
				GD.Print($"[Diverter] {Name}: FC-Avanço = {fcAvancoAtivo}");
			}
		}
		
		if ((fcRecuoAtivo != lastFcRecuoState || forceWrite) && !string.IsNullOrEmpty(tagFcRecuo))
		{
			Main.Write(tagFcRecuo, fcRecuoAtivo);
			lastFcRecuoState = fcRecuoAtivo;
			
			if (Main.DebugOpcEvents)
			{
				GD.Print($"[Diverter] {Name}: FC-Recuo = {fcRecuoAtivo}");
			}
		}
	}

	// Helper para buscar múltiplas tags do mesmo componente
	private List<SceneComponents> GetAllComponentsByKey(string key, int scene)
	{
		List<SceneComponents> allComponents = scene switch
		{
			1 => SceneComponents.sceneOneComponents,
			2 => SceneComponents.sceneTwoComponents,
			3 => SceneComponents.sceneThreeComponents,
			4 => SceneComponents.sceneFourComponents,
			5 => SceneComponents.sceneFiveComponents,
			6 => SceneComponents.sceneSixComponents,
			_ => new List<SceneComponents>()
		};
		
		return allComponents.Where(c => c.Key.ToLower() == key.ToLower()).ToList();
	}
}
