extends CharacterBody3D

@export var max_speed: float = 10.0
@export var acceleration: float = 25.0
@export var mouse_sensitivity: float = 0.003

@export var camera: Camera3D

# Controle de modo voo
var is_flying: bool = false

# Rotação da câmera
var camera_rotation: Vector2 = Vector2.ZERO

func _ready() -> void:
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)

func _input(event: InputEvent) -> void:
	# Captura movimento do mouse quando botão direito está pressionado
	if event is InputEventMouseMotion and is_flying:
		rotate_camera(event.relative)
	
	# Detecta pressionar/soltar botão direito do mouse
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_RIGHT:
			if event.pressed:
				is_flying = true
				Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
			else:
				is_flying = false
				Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)

func rotate_camera(mouse_delta: Vector2) -> void:
	# Rotação horizontal (yaw) - gira o corpo do player
	rotate_y(-mouse_delta.x * mouse_sensitivity)
	
	# Rotação vertical (pitch) - inclina a câmera
	camera_rotation.x -= mouse_delta.y * mouse_sensitivity
	camera_rotation.x = clamp(camera_rotation.x, -PI/2, PI/2)
	
	# Aplica rotação na câmera
	if camera:
		camera.rotation.x = camera_rotation.x

func _physics_process(delta: float) -> void:
	# Só movimenta se botão direito estiver pressionado
	if not is_flying:
		velocity.x = move_toward(velocity.x, 0, delta * acceleration)
		velocity.y = move_toward(velocity.y, 0, delta * acceleration)
		velocity.z = move_toward(velocity.z, 0, delta * acceleration)
		move_and_slide()
		return
	
	## --- Movement Input (CORRIGIDO) --- ##
	# ✅ Ordem correta: A/D (esquerda/direita) e W/S (frente/trás)
	var input_dir := Input.get_vector("move_left", "move_right", "move_back", "move_forward")
	
	## --- Direção de Movimento (baseada na câmera) --- ##
	var direction := Vector3.ZERO
	
	# ✅ W/S - Movimento para frente/trás seguindo a direção da câmera
	if input_dir.y != 0:  # W ou S pressionado
		# -camera.global_transform.basis.z = para frente da câmera
		direction += -camera.global_transform.basis.z * input_dir.y
	
	# ✅ A/D - Movimento lateral seguindo a direção da câmera
	if input_dir.x != 0:  # A ou D pressionado
		# camera.global_transform.basis.x = direita da câmera
		direction += camera.global_transform.basis.x * input_dir.x
	
	# Normaliza para evitar movimento mais rápido na diagonal
	direction = direction.normalized()
	
	## --- Aplica Movimento --- ##
	if direction != Vector3.ZERO:
		var target_velocity := direction * max_speed
		velocity.x = move_toward(velocity.x, target_velocity.x, delta * acceleration)
		velocity.y = move_toward(velocity.y, target_velocity.y, delta * acceleration)
		velocity.z = move_toward(velocity.z, target_velocity.z, delta * acceleration)
	else:
		# Para gradualmente quando não há input
		velocity.x = move_toward(velocity.x, 0, delta * acceleration)
		velocity.y = move_toward(velocity.y, 0, delta * acceleration)
		velocity.z = move_toward(velocity.z, 0, delta * acceleration)
	
	move_and_slide()
