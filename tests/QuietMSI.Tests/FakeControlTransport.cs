using QuietMSI.Core;
internal sealed class FakeControlTransport(HardwareControlState state) : IControlTransport
{
 public HardwareControlState State=state; public List<string> Events=new(); public bool Conflict,FailPost; public Func<int,HardwareControlState,HardwareControlState>? OnRead; private int reads;
 public void VerifyIdentity() { Events.Add("Identity"); }
 public HardwareControlState ReadState() { Events.Add("Read"); reads++; if(OnRead is not null)return OnRead(reads,State);return Conflict && reads>1 ? State with {Boost=7}:State; }
 public void Record(string stage,object data) { Events.Add(stage); }
 public void Write(RegisterChange change) { Events.Add("Write"); if(FailPost)return;State=change.Offset switch {0x98=>State with {Boost=change.After},0xd2=>State with {Performance=change.After},0xd4=>State with {FanMode=change.After},_=>throw new Exception()}; }
}
