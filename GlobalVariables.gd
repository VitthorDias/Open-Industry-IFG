extends Node

var opc_da_connected: bool = false
var GLOBAL_UPDATE_RATE: int = 1000
var main : Node

signal simulation_started
signal simulation_set_paused(paused)
signal simulation_ended
signal opc_da_comms_connected
