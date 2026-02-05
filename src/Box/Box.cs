using Godot;
using System;
using System.Collections.Generic;

public partial class Box : Node3D
{	
	// Sistema de Grab
	private bool isBeingGrabbed = false;
	private Camera3D camera;
	private float grabDistance = 3.0f;
	private Vector3 grabOffset = Vector3.Zero;

	// Sistema de inércia
	private Vector3 previousGrabPosition = Vector3.Zero;
	private Vector3 grabVelocity = Vector3.Zero;
	private const int velocitySamples = 5; // Quantidade de amostras para suavizar velocidade
	private List<Vector3> velocityHistory = new List<Vector3>();

	RigidBody3D rigidBody;
	Vector3 initialPos;
	public bool instanced = false;
	bool keyHeld = false;

	Root Main;
	
	private float _mass = 1.0f; // Cache do valor

	public float Mass
	{
		get
		{
			// Se RigidBody já existe, retorna valor dele
			if (rigidBody != null)
				return rigidBody.Mass;
			
			// Senão retorna valor em cache
			return _mass;
		}
		set
		{
			_mass = value; // Sempre salva em cache
			
			// Se RigidBody já existe, aplica imediatamente
			if (rigidBody != null)
			{
				rigidBody.Mass = value;
				
				// GD.Print($"[Box] Mass aplicada: {value:F2} kg (RigidBody pronto)");
			}
			else
			{
				// GD.Print($"[Box] Mass salva em cache: {value:F2} kg (RigidBody ainda não inicializado)");
			}
		}
	}
	
	public override void _Ready()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			return;
		}

		Main.SimulationStarted += Set;
		Main.SimulationEnded += Reset;
		Main.SimulationSetPaused += OnSetPaused;

		rigidBody = GetNode<RigidBody3D>("RigidBody3D");

		camera = GetViewport().GetCamera3D();

		if (_mass != 1.0f && rigidBody != null)
		{
			rigidBody.Mass = _mass;
			GD.Print($"[Box] Mass do cache aplicada no _Ready(): {_mass:F2} kg");
		}

		SetPhysicsProcess(false);
	}
	
	public override void _EnterTree()
	{
		if (Main != null && !instanced)
		{
			Main.SimulationStarted += Set;
			Main.SimulationEnded += Reset;
			Main.SimulationSetPaused += OnSetPaused;
		}
	}
	
	public override void _ExitTree()
	{
		if(Main == null) return;

		Main.SimulationStarted -= Set;
		Main.SimulationEnded -= Reset;
		Main.SimulationSetPaused -= OnSetPaused;
		
		if (instanced) QueueFree();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Main == null) return;

		if (isBeingGrabbed)
		{
			UpdateGrabbedPosition();
			return; // Não processa resto da lógica
		}

		if (!Input.IsPhysicalKeyPressed(Key.G))
		{
			keyHeld = false;
		}

		if (rigidBody.Freeze)
		{
			rigidBody.TopLevel = false;
			rigidBody.Position = Vector3.Zero;
			rigidBody.Rotation = Vector3.Zero;
			rigidBody.Scale = Vector3.One;
		}
		else
		{
			rigidBody.TopLevel = true;
			Position = rigidBody.Position;
			Rotation = rigidBody.Rotation;
			Scale = rigidBody.Scale;
		}
	}
	
	void Set()
	{
		if (Main == null) return;
		
		initialPos = GlobalPosition;
		rigidBody.TopLevel = true;
		rigidBody.Freeze = false;
		SetPhysicsProcess(true);
	}
	
	void Reset()
	{
		if (isBeingGrabbed)
		{
			ReleaseBox();
		}

		if (instanced)
		{
			Main.SimulationStarted -= Set;
			Main.SimulationEnded -= Reset;
			Main.SimulationSetPaused -= OnSetPaused;
			QueueFree();
		}
		else
		{
			SetPhysicsProcess(false);
			rigidBody.TopLevel = false;
			
			rigidBody.Position = Vector3.Zero;
			rigidBody.Rotation = Vector3.Zero;
			rigidBody.Scale = Vector3.One;
			
			rigidBody.LinearVelocity = Vector3.Zero;
			rigidBody.AngularVelocity = Vector3.Zero;
			
			GlobalPosition = initialPos;
			Rotation = Vector3.Zero;
		}
	}
	
	void OnSetPaused(bool paused)
	{
		rigidBody.Freeze = paused;
	}
	
	public void SetNewOwner(Root newOwner)
	{
		instanced = true;
		Owner = newOwner;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Só funciona durante simulação rodando
		if (Main == null || !Main.Start)
			return;
		
		// Clique esquerdo do mouse
		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (mouseButton.Pressed)
			{
				TryGrabBox();
			}
			else
			{
				ReleaseBox();
			}
		}
	}

	private void TryGrabBox()
	{
		if (camera == null || isBeingGrabbed)
			return;
		
		// Raycast da câmera para ver se clicou nesta caixa
		var spaceState = GetWorld3D().DirectSpaceState;
		var mousePos = GetViewport().GetMousePosition();
		
		var from = camera.ProjectRayOrigin(mousePos);
		var to = from + camera.ProjectRayNormal(mousePos) * 100f;
		
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		var result = spaceState.IntersectRay(query);
		
		if (result.Count > 0)
		{
			var hitBody = result["collider"].AsGodotObject();
			
			// Verifica se clicou no RigidBody desta caixa
			if (hitBody == rigidBody)
			{
				isBeingGrabbed = true;
				
				// Congela física
				rigidBody.Freeze = true;
				
				// Calcula distância inicial
				grabDistance = camera.GlobalPosition.DistanceTo(rigidBody.GlobalPosition);
				grabOffset = rigidBody.GlobalPosition - GetGrabPosition();

				previousGrabPosition = rigidBody.GlobalPosition;
				grabVelocity = Vector3.Zero;
				velocityHistory.Clear();
			}
		}
	}

	private void ReleaseBox()
	{
		if (!isBeingGrabbed)
			return;
		
		isBeingGrabbed = false;
		
		// Descongela física
		rigidBody.Freeze = false;

		if (velocityHistory.Count > 0)
		{
			// Calcula média das últimas velocidades para suavizar
			Vector3 avgVelocity = Vector3.Zero;
			foreach (var vel in velocityHistory)
			{
				avgVelocity += vel;
			}
			avgVelocity /= velocityHistory.Count;
			
			// Aplica velocidade ao RigidBody
			rigidBody.LinearVelocity = avgVelocity * 0.5f;
		}
	}

	private Vector3 GetGrabPosition()
	{
		if (camera == null)
			return GlobalPosition;
		
		var mousePos = GetViewport().GetMousePosition();
		var from = camera.ProjectRayOrigin(mousePos);
		var direction = camera.ProjectRayNormal(mousePos);
		
		return from + direction * grabDistance;
	}

	private void UpdateGrabbedPosition()
	{
		var targetPos = GetGrabPosition() + grabOffset;
		
		// Move suavemente para posição alvo
		rigidBody.GlobalPosition = rigidBody.GlobalPosition.Lerp(targetPos, 0.3f);

		Vector3 currentVelocity = (rigidBody.GlobalPosition - previousGrabPosition) / (float)GetPhysicsProcessDeltaTime();
		
		velocityHistory.Add(currentVelocity);
		if (velocityHistory.Count > velocitySamples)
		{
			velocityHistory.RemoveAt(0);
		}
		
		// Atualiza posição anterior
		previousGrabPosition = rigidBody.GlobalPosition;
		
		// Ajusta distância com Page Up/Down (opcional)
		if (Input.IsActionPressed("ui_page_up"))
		{
			grabDistance = Mathf.Max(grabDistance - 0.1f, 1f);
		}
		else if (Input.IsActionPressed("ui_page_down"))
		{
			grabDistance = Mathf.Min(grabDistance + 0.1f, 20f);
		}
	}
}