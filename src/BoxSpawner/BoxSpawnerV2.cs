using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BoxSpawnerV2 : Node3D
{
	[ExportGroup("Box Variant 1")]
	[Export] public PackedScene BoxScene1 { get; set; }
	[Export] public float BoxWeight1 { get; set; } = 2.5f;
	[Export] public float BoxProbability1 { get; set; } = 0.33f;

	[ExportGroup("Box Variant 2")]
	[Export] public PackedScene BoxScene2 { get; set; }
	[Export] public float BoxWeight2 { get; set; } = 5.0f;
	[Export] public float BoxProbability2 { get; set; } = 0.34f;

	[ExportGroup("Box Variant 3")]
	[Export] public PackedScene BoxScene3 { get; set; }
	[Export] public float BoxWeight3 { get; set; } = 7.5f;
	[Export] public float BoxProbability3 { get; set; } = 0.33f;

	[ExportGroup("Spawn Settings")]
	[Export] public float SpawnInterval { get; set; } = 1f;

	// Classe interna para organizar variantes
	private class BoxVariant
	{
		public PackedScene Scene { get; set; }
		public float Weight { get; set; }
		public float Probability { get; set; }
	}

	// Lista interna de variantes (construída em runtime)
	private List<BoxVariant> boxVariants = new List<BoxVariant>();

	// Estado
	private bool running = false;

	// Timer
	private float spawnTimer = 0f;

	// Componentes
	private Area3D spawnArea;
	private Vector3 spawnAreaOrigin;

	// Estatísticas (debug)
	private Dictionary<float, int> spawnedWeightStats = new Dictionary<float, int>();

	// Referências
	private Root Main;

	public override void _Ready()
	{	
		Inicializacao_Componentes();
		Construir_Lista_Variantes();
		Validar_Configuracao();
		Conectar_Root();
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (Main == null) return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
	}

	private void Inicializacao_Componentes()
	{
		spawnArea = GetNode<Area3D>("SpawnArea");

		if (spawnArea == null)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: SpawnArea não encontrada!");
		}
		else
		{
			spawnAreaOrigin = spawnArea.Position;
		}
	}

	private void Construir_Lista_Variantes()
	{
		boxVariants.Clear();

		// Adiciona variante 1 (se configurada)
		if (BoxScene1 != null)
		{
			boxVariants.Add(new BoxVariant 
			{ 
				Scene = BoxScene1, 
				Weight = BoxWeight1, 
				Probability = BoxProbability1 
			});
		}

		// Adiciona variante 2 (se configurada)
		if (BoxScene2 != null)
		{
			boxVariants.Add(new BoxVariant 
			{ 
				Scene = BoxScene2, 
				Weight = BoxWeight2, 
				Probability = BoxProbability2 
			});
		}

		// Adiciona variante 3 (se configurada)
		if (BoxScene3 != null)
		{
			boxVariants.Add(new BoxVariant 
			{ 
				Scene = BoxScene3, 
				Weight = BoxWeight3, 
				Probability = BoxProbability3 
			});
		}
	}

	private void Validar_Configuracao()
	{
		if (boxVariants.Count == 0)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: Nenhuma variante de caixa configurada!");
			return;
		}

		// Normaliza probabilidades (garante que somam 1.0)
		float sum = boxVariants.Sum(v => v.Probability);
		
		if (Mathf.Abs(sum - 1.0f) > 0.01f)
		{
			GD.Print($"[BoxSpawnerV2] {Name}: Normalizando probabilidades (soma era {sum:F2})");
			
			foreach (var variant in boxVariants)
				variant.Probability /= sum;
		}

		// Inicializa estatísticas
		foreach (var variant in boxVariants)
		{
			if (!spawnedWeightStats.ContainsKey(variant.Weight))
				spawnedWeightStats[variant.Weight] = 0;
		}

		// Log de configuração
		GD.Print($"[BoxSpawnerV2] {Name}: {boxVariants.Count} variante(s) configurada(s):");
		foreach (var variant in boxVariants)
		{
			GD.Print($"  {variant.Weight:F1}kg → {variant.Probability * 100:F1}% ({variant.Scene?.ResourcePath ?? "NULL"})");
		}
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: Root não encontrado!");
			return;
		}
		
		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
	}
	
	private void OnSimulationStarted()
	{
		if (Main == null || boxVariants.Count == 0) 
			return;
		
		if (Main.DebugOpcEvents)
		{
			GD.Print($"[BoxSpawnerV2] {Name}: Iniciando spawner");
			GD.Print($"  Variantes disponíveis:");
			
			foreach (var variant in boxVariants)
			{
				GD.Print($"    {variant.Weight:F1}kg ({variant.Probability * 100:F1}%)");
			}
		}

		running = true;
		spawnTimer = 0f;

		// Reset estatísticas
		foreach (var key in spawnedWeightStats.Keys.ToList())
			spawnedWeightStats[key] = 0;

		SetProcess(true);
		
		if (!IsBoxInsideSpawnArea())
		{	
			SpawnBox();
		}
	}

	private void OnSimulationEnded()
	{
		running = false;
		spawnTimer = 0f;

		SetProcess(false);

		// Mostra estatísticas finais
		if (Main.DebugOpcEvents && spawnedWeightStats.Count > 0)
		{
			GD.Print($"[BoxSpawnerV2] {Name}: Estatísticas de spawn:");
			
			int total = spawnedWeightStats.Values.Sum();
			
			foreach (var kvp in spawnedWeightStats.OrderBy(x => x.Key))
			{
				float percentage = total > 0 ? (kvp.Value / (float)total) * 100 : 0;
				GD.Print($"  {kvp.Key:F1}kg: {kvp.Value} caixas ({percentage:F1}%)");
			}
		}

		Limpar_Caixas_Spawn();
	}
	
	public override void _Process(double delta)
	{
		if (!running || Main == null)
			return;

		spawnTimer += (float)delta;

		if (spawnTimer >= SpawnInterval)
		{
			spawnTimer = 0f;
			
			// Cria caixa somente se não tiver caixa na área de Spawn
			if (!IsBoxInsideSpawnArea())
			{
				SpawnBox();
			}
			else if (Main.DebugOpcEvents)
			{
				GD.Print($"[BoxSpawnerV2] {Name}: Área ocupada, aguardando...");
			}
		}
	}

	private bool IsBoxInsideSpawnArea()
	{
		if (spawnArea == null)
			return false;

		spawnArea.Position = spawnAreaOrigin;
		spawnArea.Rotation = Vector3.Zero;
		spawnArea.Scale = Vector3.One;

		var overlappingBodies = spawnArea.GetOverlappingBodies();

		if (overlappingBodies.Count == 0)
			return false;
		
		foreach (var body in overlappingBodies)
		{
			var owner = body.GetParent();

			if (owner is Box box && box.instanced)
				return true;
		}
	
		return false;
	}

	private void SpawnBox()
	{
		if (boxVariants.Count == 0)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: Nenhuma variante configurada!");
			return;
		}

		// Seleciona variante baseada nas probabilidades
		BoxVariant selectedVariant = Selecionar_Variante_Aleatoria();

		if (selectedVariant == null || selectedVariant.Scene == null)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: Variante selecionada é inválida!");
			return;
		}

		var box = selectedVariant.Scene.Instantiate<Box>();

		if (box == null)
		{
			GD.PrintErr($"[BoxSpawnerV2] {Name}: Falha ao instanciar Box!");
			return;
		}

		// Define peso (a cena já tem o tamanho correto)
		box.Mass = selectedVariant.Weight;
		
		// Atualiza estatísticas
		if (spawnedWeightStats.ContainsKey(selectedVariant.Weight))
			spawnedWeightStats[selectedVariant.Weight]++;

		AddChild(box, forceReadableName: true);
		box.SetNewOwner(Main);
		box.SetPhysicsProcess(true);
		box.Position = GlobalPosition;
		
		if (Main.DebugOpcEvents)
		{
			GD.Print($"[BoxSpawnerV2] {Name}: Caixa spawned → {selectedVariant.Weight:F1}kg | Pos: {box.Position}");
		}

		// GD.Print($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
		// GD.Print($"[BoxSpawnerV2] 📦 CAIXA SPAWNED");
		// GD.Print($"  Peso: {selectedVariant.Weight:F2} kg");
		// GD.Print($"  Scale: {box.Scale}");
		// GD.Print($"  Posição: {box.Position}");
		// GD.Print($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
	}

	private BoxVariant Selecionar_Variante_Aleatoria()
	{
		// Seleciona variante baseada nas probabilidades configuradas
		float random = (float)GD.Randf();
		float cumulative = 0f;

		foreach (var variant in boxVariants)
		{
			cumulative += variant.Probability;
			
			if (random <= cumulative)
				return variant;
		}

		// Fallback: retorna última variante
		return boxVariants[boxVariants.Count - 1];
	}

	private void Limpar_Caixas_Spawn()
	{
		int count = 0;

		foreach (Node child in GetChildren())
		{
			if (child is Box box)
			{
				box.QueueFree();
				count++;
			}
		}
		
		if (count > 0 && Main.DebugOpcEvents)
		{
			GD.Print($"[BoxSpawnerV2] {Name}: {count} caixa(s) removida(s)");
		}
	}
}