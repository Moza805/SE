using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private readonly MyCommandLine _commandLine = new MyCommandLine();
        private readonly List<Airlock> _runningAirlocks = new List<Airlock>();
        private readonly Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        public Program()
        {
            _commands["cycle"] = Cycle;
            _commands["enter"] = Enter;
            _commands["exit"] = Exit;
            _commands["running"] = Running;

            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Action commandAction;

            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // Someone pushed a button, process the associated command
            // We're waiting for [de]pressurization to finish
            if (updateSource == UpdateType.Update100)
            {
                CheckRunningAirlocks();
            }
            else
            {
                if (_commandLine.TryParse(argument))
                {
                    string command = _commandLine.Argument(0);
                    if (command == null)
                    {
                        return;
                    }
                    else if (_commands.TryGetValue(command, out commandAction))
                    {
                        commandAction();
                    }
                }
            }
        }

        private void CheckRunningAirlocks()
        {
            for (var i = _runningAirlocks.Count - 1; i >= 0; i--)
            {
                var airlock = _runningAirlocks[i];
                var vent = airlock.Vents[0];
                var oxygenLevel = vent.GetOxygenLevel();
                Echo($"{airlock.Name} - {Enum.GetName(typeof(VentStatus), vent.Status)}");

                // Airlock has finished pressurising, open interal door
                if (oxygenLevel >= 0.99f && (vent.Status == VentStatus.Pressurizing || vent.Status == VentStatus.Pressurized))
                {
                    Echo("Pressurised");
                    airlock.InnerDoor.OpenDoor();
                    _runningAirlocks.RemoveAt(i);
                }
                // Airlock has finished venting, open external door
                else if (oxygenLevel <= 0.00f && (vent.Status == VentStatus.Depressurized || vent.Status == VentStatus.Depressurizing))
                {
                    Echo("Depressurised");
                    airlock.OuterDoor.OpenDoor();
                    _runningAirlocks.RemoveAt(i);
                }
            }


            // No more running airlocks, stop updating.
            if (_runningAirlocks.Count == 0)
            {
                Echo("All airlocks static");
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
        }

        public void Cycle()
        {
            // Remember this airlock as we will need to monitor it
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_commandLine.Argument(1));
            var airlock = new Airlock(group);
            _runningAirlocks.Add(airlock);

            if (airlock.Vents.Count == 0)
            {
                Echo("Couldn't find the vent");
                return;
            }
            if (airlock.InnerDoor == null)
            {
                Echo("Couldn't find the inner door");
                return;
            }
            if (airlock.OuterDoor == null)
            {
                Echo("Couldn't find the outer door");
                return;
            }

            // Schedule an update in 100 ticks (~ 1.3 seconds)
            if (Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update100) == false)
            {
                Runtime.UpdateFrequency |= UpdateFrequency.Update100;
            }

            // Close doors
            airlock.InnerDoor.CloseDoor();
            airlock.OuterDoor.CloseDoor();

            var vent = airlock.Vents[0];

            // Check for leaks
            if (vent.CanPressurize == false)
            {
                Echo("Airlock is not air tight");
            }

            // Cycle the air lock
            switch (vent.Status)
            {
                case VentStatus.Depressurized:
                case VentStatus.Depressurizing:
                    vent.Depressurize = false;
                    Echo("Pressurizing");
                    break;
                case VentStatus.Pressurized:
                case VentStatus.Pressurizing:
                    vent.Depressurize = true;
                    Echo("Depressurizing");
                    break;
            }

        }

        public void Enter()
        {
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_commandLine.Argument(1));
            var airlock = new Airlock(group);
            _runningAirlocks.Add(airlock);

            if (airlock.Vents.Count == 0)
            {
                Echo("Couldn't find the vent");
                return;
            }
            if (airlock.InnerDoor == null)
            {
                Echo("Couldn't find the inner door");
                return;
            }
            if (airlock.OuterDoor == null)
            {
                Echo("Couldn't find the outer door");
                return;
            }

            // Schedule an update in 100 ticks (~ 1.3 seconds)
            if (Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update100) == false)
            {
                Runtime.UpdateFrequency |= UpdateFrequency.Update100;
            }

            airlock.OuterDoor.CloseDoor();
            airlock.InnerDoor.CloseDoor();
            airlock.Vents[0].Depressurize = true;
        }

        public void Exit()
        {
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(_commandLine.Argument(1));
            var airlock = new Airlock(group);

            if (airlock.Vents.Count == 0)
            {
                Echo("Couldn't find the vent");
                return;
            }
            if (airlock.InnerDoor == null)
            {
                Echo("Couldn't find the inner door");
                return;
            }
            if (airlock.OuterDoor == null)
            {
                Echo("Couldn't find the outer door");
                return;
            }

            _runningAirlocks.Add(airlock);

            // Schedule an update in 100 ticks (~ 1.3 seconds)
            if (Runtime.UpdateFrequency.HasFlag(UpdateFrequency.Update100) == false)
            {
                Runtime.UpdateFrequency |= UpdateFrequency.Update100;
            }

            airlock.OuterDoor.CloseDoor();
            airlock.InnerDoor.CloseDoor();
            airlock.Vents[0].Depressurize = false;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Running()
        {
            if (_runningAirlocks.Count > 0)
            {
                foreach (var airlock in _runningAirlocks)
                {
                    Echo($"{airlock.Name} - {airlock.Vents[0].GetOxygenLevel():0.00}");
                }
            }
            else
            {
                Echo("No airlocks currently cycling");
            }
        }
    }

    public class Airlock
    {
        public string Name = String.Empty;
        public List<IMyAirVent> Vents { get; private set; } = new List<IMyAirVent>();
        public List<IMyDoor> Doors { get; private set; } = new List<IMyDoor>();

        public IMyDoor InnerDoor
        {
            get
            {
                return getDoorByName("inner");
            }
        }

        public IMyDoor OuterDoor
        {
            get
            {
                return getDoorByName("outer");
            }
        }

        public Airlock(IMyBlockGroup group)
        {
            group.GetBlocksOfType(Vents);
            group.GetBlocksOfType(Doors);
            Name = group.Name;
        }

        private IMyDoor getDoorByName(string doorName)
        {
            foreach (var door in Doors)
            {
                if (door.CustomName.Contains(doorName))
                {
                    return door;
                }
            }
            return null;
        }
    }
}
