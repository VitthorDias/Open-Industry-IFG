using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public partial class BalanceConveyor : Node3D, IBeltConveyor
{
	public enum OutputSignalType
	{
		Current_4_20mA,		// Sinal: 4-20mA
		Voltage_0_10V,		// Sinal: 0-10V
		Digital_Int15		// Sinal: 0 a 32767
	}

	[Export]
	public Color BeltColor
	{
		get
		{
			return beltColor;
		}
		set
		{
			beltColor = value;
			Atualizar_Cor_Belt();
		}
	}

	[Export]
	public IBeltConveyor.ConvTexture BeltTexture
	{
		get
		{
			return beltTexture;
		}
		set
		{
			beltTexture = value;
			Atualizar_Textura_Belt();
		}
	}

	[Export]
	public float Speed { get; set; }

	[Export]
	public float FixedSpeed { get; set; } = 2.0f;

	[ExportGroup("Weight Measurement")]
	[Export]
	public float MaxWeight { get; set; } = 100f; // Peso Máximo: 100 kg (Valor Padrão)
	[Export]
	public OutputSignalType SignalType { get; set; } = OutputSignalType.Current_4_20mA;
	[Export]
	public float WeightSampleRate { get; set; } = 0.1f; // Verifica o peso a cada 100ms

	[ExportSubgroup("Fluctuation Settings")]
	[Export] public float StaticNoiseAmplitude { get; set; } = 0.0005f; // ±0.05% quando parada (±0.1kg em 5kg)
	[Export] public float MovingNoiseAmplitude { get; set; } = 0.08f; // ±8% quando em movimento (±0.4kg em 5kg)
	[Export] public float NoiseFrequency { get; set; } = 5.0f; // Frequência da oscilação (Hz)

	[ExportGroup("Visual Animation")]
	[Export] public bool EnableWeightAnimation { get; set; } = true;
	[Export] public float MaxDepression { get; set; } = 0.01f; // 1cm de depressão máxima
	[Export] public float AnimationSpeed { get; set; } = 5.0f; // Velocidade da animação (suave)

	// Constantes
	private const float MIN_WEIGHT = 0f; // Peso Mínimo: 0 kg
	
	// Visual
	private Color beltColor = new Color(1, 1, 1, 1);
	private IBeltConveyor.ConvTexture beltTexture = IBeltConveyor.ConvTexture.Standard;
	private float targetDepression = 0f;
	private float currentDepression = 0f;
	private Vector3 originalRbPosition;
	
	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	public double beltPosition = 0.0;
	
	// Peso (balança)
	private float currentWeight = 0f;
	private float lastSentWeight = -1f;
	private double weightSampleTimer = 0.0;

	// Sistema de flutuação na leitura do peso
	private float noiseOffset = 0f;
	private double noiseTime = 0.0;
	private bool isConveyorMoving = false;
	
	// Tags OPC (2 tags: comando + peso)
	private string opcTagSpeed = "";
	private string opcTagWeight = "";
	
	// Componentes 3D
	private RigidBody3D rb;
	private MeshInstance3D mesh;
	private Material beltMaterial;
	private Material metalMaterial;
	private ConveyorEnd conveyorEnd1;
	private ConveyorEnd conveyorEnd2;
	
	// Área de detecção de peso
	private Area3D weightArea;
	
	// Física
	private Vector3 origin;

	// Referências
	public Root Main { get; set; }
	private int currentScene;

	public override void _Ready()
	{
		Inicializacao_Componentes3D();
		Inicializacao_Materials();
		Inicializacao_Deteccao_Peso();
		Conectar_Root();
	}

	public override void _ExitTree()
	{
		if (Main == null) return;
		
		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
		Main.OpcDataChanged -= OnOpcDataReceived;
		
		if (weightArea != null)
		{
			weightArea.BodyEntered -= OnBodyEntered;
			weightArea.BodyExited -= OnBodyExited;
		}
	}

	private void Inicializacao_Componentes3D()
	{
		rb = GetNode<RigidBody3D>("RigidBody3D");
		mesh = GetNode<MeshInstance3D>("RigidBody3D/MeshInstance3D");

		mesh.Mesh = mesh.Mesh.Duplicate() as Mesh;
		beltMaterial = mesh.Mesh.SurfaceGetMaterial(0).Duplicate() as Material;
		metalMaterial = mesh.Mesh.SurfaceGetMaterial(1).Duplicate() as Material;
		
		mesh.Mesh.SurfaceSetMaterial(0, beltMaterial);
		mesh.Mesh.SurfaceSetMaterial(1, metalMaterial);
		mesh.Mesh.SurfaceSetMaterial(2, metalMaterial);

		conveyorEnd1 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd");
		conveyorEnd2 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd2");

		origin = rb.Position;
		originalRbPosition = rb.Position;
	}
	
	private void Inicializacao_Materials()
	{
		((ShaderMaterial)beltMaterial).SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
		conveyorEnd1.beltMaterial.SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
		conveyorEnd2.beltMaterial.SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
		
		((ShaderMaterial)beltMaterial).SetShaderParameter("ColorMix", beltColor);
		conveyorEnd1.beltMaterial.SetShaderParameter("ColorMix", beltColor);
		conveyorEnd2.beltMaterial.SetShaderParameter("ColorMix", beltColor);
	}

	private void Inicializacao_Deteccao_Peso()
	{
		// Tenta encontrar WeightArea existente
		weightArea = rb.GetNodeOrNull<Area3D>("WeightArea");
		
		if (weightArea == null)
		{
			GD.Print($"[BalanceConveyor] {Name}: WeightArea não encontrada, criando...");
			
			// Busca o CollisionShape3D existente para copiar dimensões
			var existingCollision = rb.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
			
			if (existingCollision?.Shape is BoxShape3D existingBox)
			{
				// Cria WeightArea como filho do RigidBody3D
				weightArea = new Area3D { Name = "WeightArea" };
				rb.AddChild(weightArea);
				
				// Cria CollisionShape copiando dimensões do existente
				var collisionShape = new CollisionShape3D();
				var weightBox = new BoxShape3D 
				{ 
					// Copia largura/profundidade, aumenta altura para melhor detecção
					Size = new Vector3(
						existingBox.Size.X, 
						existingBox.Size.Y + 0.3f,  // +30cm de altura
						existingBox.Size.Z
					) 
				};
				collisionShape.Shape = weightBox;
				collisionShape.Position = new Vector3(0, existingBox.Size.Y * 0.25f, 0); // Ajusta posição Y
				weightArea.AddChild(collisionShape);
				
				// Configuração de camadas
				weightArea.CollisionLayer = 0;
				weightArea.CollisionMask = 1; // Detecta objetos na layer 1
				
				// GD.Print($"[BalanceConveyor] {Name}: WeightArea criada (dimensões copiadas)");
			}
			else
			{
				// GD.PrintErr($"[BalanceConveyor] {Name}: CollisionShape3D não encontrado! Criando com dimensões padrão.");
				
				// Fallback: cria com dimensões padrão
				weightArea = new Area3D { Name = "WeightArea" };
				rb.AddChild(weightArea);
				
				var collisionShape = new CollisionShape3D();
				var boxShape = new BoxShape3D { Size = new Vector3(1, 0.5f, 1) };
				collisionShape.Shape = boxShape;
				weightArea.AddChild(collisionShape);
				
				weightArea.CollisionLayer = 0;
				weightArea.CollisionMask = 1;
			}
		}
		
		// Conecta eventos
		weightArea.BodyEntered += OnBodyEntered;
		weightArea.BodyExited += OnBodyExited;
	}
	
	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[BalanceConveyor] {Name}: Root não encontrado!");
			return;
		}

		currentScene = Main.currentScene;

		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
		Main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		// Busca os 2 componentes separados
		var components = GetAllComponentsByKey(Name, currentScene);
		
		foreach (var component in components)
		{
			if (string.IsNullOrEmpty(component.Tag)) continue;
			
			if (component.Type == "input")
			{
				// INPUT = envia peso para PLC
				opcTagWeight = component.Tag;
			}
			else if (component.Type == "output")
			{
				// OUTPUT = recebe bool (liga/desliga) do PLC
				opcTagSpeed = component.Tag;
			}
		}
		
		// Validação
		if (string.IsNullOrEmpty(opcTagSpeed))
		{
			GD.PrintErr($"[BalanceConveyor] {Name}: Tag de velocidade (OUTPUT) não configurada!");
		}
		
		if (string.IsNullOrEmpty(opcTagWeight))
		{
			GD.PrintErr($"[BalanceConveyor] {Name}: Tag de peso (INPUT) não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[BalanceConveyor] {Name}: OPC conectado");
			GD.Print($"   OUTPUT (recebe liga/desliga) → {opcTagSpeed}");
			GD.Print($"   INPUT (envia peso) → {opcTagWeight} ({SignalType})");
			GD.Print($"   Range: {MIN_WEIGHT}-{MaxWeight}kg");
			GD.Print($"   Velocidade fixa: {FixedSpeed}");
		}
		
		// Reset de estado
		running = true;
		Speed = 0f;
		beltPosition = 0.0;
		currentWeight = 0f;
		lastSentWeight = -1f;
		weightSampleTimer = 0.0;
	}

	private void OnSimulationEnded()
	{
		running = false;
		Speed = 0f;
		beltPosition = 0;
		currentWeight = 0f;
		lastSentWeight = -1f;

		currentDepression = 0f;
		targetDepression = 0f;
		origin = originalRbPosition;
		
		((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition);
		
		rb.Position = Vector3.Zero;
		rb.Rotation = Vector3.Zero;
		rb.LinearVelocity = Vector3.Zero;
		
		foreach (Node3D child in rb.GetChildren())
		{
			child.Position = Vector3.Zero;
			child.Rotation = Vector3.Zero;
		}
	}

	private void OnOpcDataReceived(string tagName, object value)
    {
    	if (tagName != opcTagSpeed || value == null) return;
		
		try
		{
			float newSpeed = Convert.ToSingle(value); // Velocidade variável
			
			if (Mathf.Abs(newSpeed - Speed) > 0.01f)
			{
				Speed = newSpeed;

				// Atualiza flag de movimento (para flutuação do peso)
				isConveyorMoving = Speed > 0.01f;
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[BalanceConveyor] {Name}: Speed = {Speed:F2}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[BalanceConveyor] {Name}: Erro ao converter estado:");
			GD.PrintErr(e.Message);
		}
    }

	private void OnBodyEntered(Node3D body)
	{
		if (!running) return;
		
		if (body is RigidBody3D rigidBody)
		{
			float objectMass = rigidBody.Mass;
			currentWeight += objectMass;

			Atualizar_Peso_Depressao();
			
			if (Main.DebugOpcEvents)
			{
				GD.Print($"[BalanceConveyor] {Name}: Objeto entrou (+{objectMass:F2}kg) → Total: {currentWeight:F2}kg");
			}
		}
	}
	
	private void OnBodyExited(Node3D body)
	{
		if (!running) return;
		
		if (body is RigidBody3D rigidBody)
		{
			float objectMass = rigidBody.Mass;
			currentWeight -= objectMass;
			
			if (currentWeight < 0) currentWeight = 0;

			Atualizar_Peso_Depressao();
			
			if (Main.DebugOpcEvents)
			{
				GD.Print($"[BalanceConveyor] {Name}: Objeto saiu (-{objectMass:F2}kg) → Total: {currentWeight:F2}kg");
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Main == null || !running) return;
		
		Atualizar_Animacao_Visual(delta);
		Atualizar_Movimento_Belt(delta);
		Atualizar_Escala_Shader();
		Atualizar_Output_Peso(delta);
	}

	private void Atualizar_Peso_Depressao()
	{
		if (!EnableWeightAnimation)
		{
			targetDepression = 0f;
			return;
		}
		
		// Calcula depressão baseada no peso (proporcional ao MaxWeight)
		float weightRatio = Mathf.Clamp(currentWeight / MaxWeight, 0f, 1f);
		targetDepression = MaxDepression * weightRatio;
	}

	private void Atualizar_Animacao_Visual(double delta)
	{
		if (!EnableWeightAnimation)
		{
			// Se animação desabilitada, garante posição original
			if (currentDepression != 0f)
			{
				currentDepression = 0f;
				origin = originalRbPosition;
			}
			return;
		}
		
		// Interpola suavemente entre depressão atual e alvo
		currentDepression = Mathf.Lerp(currentDepression, targetDepression, AnimationSpeed * (float)delta);
		
		// Aplica depressão no eixo Y (para baixo)
		origin = originalRbPosition + new Vector3(0, -currentDepression, 0);
	}
	
	private void Atualizar_Movimento_Belt(double delta)
	{
		var localLeft = rb.GlobalTransform.Basis.X.Normalized();
		var velocity = localLeft * Speed;
		rb.LinearVelocity = velocity;
		rb.Position = origin;
		
		beltPosition += Speed * delta;
		if (beltPosition >= 1.0)
			beltPosition = 0.0;
		
		if (Speed != 0)
		{
			((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition * Mathf.Sign(Speed));
		}
		
		rb.Rotation = Vector3.Zero;
		rb.Scale = new Vector3(1, 1, 1);
	}

	private void Atualizar_Escala_Shader()
	{
		Scale = new Vector3(Scale.X, 1, Scale.Z);
		
		if (Speed != 0)
		{
			if (beltMaterial != null)
				((ShaderMaterial)beltMaterial).SetShaderParameter("Scale", Scale.X * Mathf.Sign(Speed));
			
			if (metalMaterial != null)
				((ShaderMaterial)metalMaterial).SetShaderParameter("Scale", Scale.X);
		}
	}

	private void Atualizar_Output_Peso(double delta)
	{
		if (!isCommsConnected || string.IsNullOrEmpty(opcTagWeight)) return;
		
		// Atualiza tempo de ruído
		noiseTime += delta;

		weightSampleTimer += delta;
		
		if (weightSampleTimer >= WeightSampleRate)
		{
			weightSampleTimer = 0.0;

			// Calcular peso de forma mais realista
			float measuredWeight = Calcular_Peso_Flutuacao();
			
			// Envia se peso mudou significativamente (0.05 kg)
			if (Mathf.Abs(measuredWeight - lastSentWeight) > 0.05f)
			{
				lastSentWeight = measuredWeight;
				
				// Converte peso para o sinal configurado
				float signalValue = Converter_Peso_Para_Sinal(measuredWeight);
				
				WriteTag_Peso_Opc(signalValue, measuredWeight);
			}
		}
	}

	private float Calcular_Peso_Flutuacao()
	{
		// Sem ruído
		if (currentWeight <= 0.01f)
			return 0f;
		
		// Seleciona amplitude de ruído baseado no estado da esteira
		float noiseAmplitude = isConveyorMoving ? MovingNoiseAmplitude : StaticNoiseAmplitude;
		
		// Gera ruído usando várias frequências
		float noise1 = Mathf.Sin((float)noiseTime * NoiseFrequency * Mathf.Pi * 2f);
		float noise2 = Mathf.Sin((float)noiseTime * NoiseFrequency * Mathf.Pi * 3.7f) * 0.5f; // Harmônico
		float noise3 = Mathf.Sin((float)noiseTime * NoiseFrequency * Mathf.Pi * 5.3f) * 0.25f; // Harmônico
		
		// Combina ruídos (normalizado)
		float combinedNoise = (noise1 + noise2 + noise3) / 1.75f;
		
		// Aplica ao peso real
		float fluctuation = currentWeight * noiseAmplitude * combinedNoise;
		float measuredWeight = currentWeight + fluctuation;
		
		// Garante não-negativo
		if (measuredWeight < 0f)
			measuredWeight = 0f;
		
		return measuredWeight;
	}

	private float Converter_Peso_Para_Sinal(float weight)
	{
		// Limita peso ao range 0 - MaxWeight
		float clampedWeight = Mathf.Clamp(weight, MIN_WEIGHT, MaxWeight);
		
		// Normaliza (0.0 a 1.0)
		float normalized = (clampedWeight - MIN_WEIGHT) / (MaxWeight - MIN_WEIGHT);		
		
		switch (SignalType)
		{
			case OutputSignalType.Current_4_20mA:
				return 4.0f + (normalized * 16.0f);
			
			case OutputSignalType.Voltage_0_10V:
				return normalized * 10.0f;
			
			case OutputSignalType.Digital_Int15:
				return normalized * 32767;
			
			default:
				return 0f;
		}
	}

	private void WriteTag_Peso_Opc(float signalValue, float measuredWeight)
	{
		try
		{
			Main.Write(opcTagWeight, signalValue); 
			
			if (Main.DebugOpcEvents)
			{
				string signalUnit = SignalType switch
				{
					OutputSignalType.Current_4_20mA => "mA",
					OutputSignalType.Voltage_0_10V => "V",
					OutputSignalType.Digital_Int15 => "bits",
					_ => ""
				};
				
				GD.Print($"[BalanceConveyor] {Name}: {currentWeight:F2}kg → {signalValue:F2}{signalUnit}");
			}

			string unit = SignalType switch
			{
				OutputSignalType.Current_4_20mA => "mA",
				OutputSignalType.Voltage_0_10V => "V",
				OutputSignalType.Digital_Int15 => "bits",
				_ => ""
			};
			
			// GD.Print($"");
			// GD.Print($"[BalanceConveyor] 📡 ENVIANDO PARA OPC");
			// GD.Print($"  Tag: {opcTagWeight}");
			// GD.Print($"  Peso real: {currentWeight:F3} kg");
			// GD.Print($"  Peso medido: {measuredWeight:F3} kg (com flutuação)");
			// GD.Print($"  Erro: {(measuredWeight - currentWeight):+0.000;-0.000} kg ({((measuredWeight - currentWeight) / currentWeight * 100):+0.0;-0.0}%)");
			// GD.Print($"  Estado esteira: {(isConveyorMoving ? "EM MOVIMENTO 🔄" : "PARADA 🛑")}");
			// GD.Print($"  Range configurado: {MIN_WEIGHT:F1} - {MaxWeight:F1} kg");
			// GD.Print($"  Tipo de sinal: {SignalType}");
			// GD.Print($"  Valor calculado: {signalValue:F3} {unit}");
			
			// float percentage = ((measuredWeight - MIN_WEIGHT) / (MaxWeight - MIN_WEIGHT)) * 100f;
			// GD.Print($"  Percentual da escala: {percentage:F1}%");
			// GD.Print($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[BalanceConveyor] {Name}: Erro ao escrever peso:");
			GD.PrintErr($"  Tag: {opcTagWeight}");
			GD.PrintErr($"  Erro: {e.Message}");
		}
	}

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

	private void Atualizar_Cor_Belt()
	{
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("ColorMix", beltColor);
	}

	private void Atualizar_Textura_Belt()
	{
		bool isStandard = beltTexture == IBeltConveyor.ConvTexture.Standard;
		
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
	}
}