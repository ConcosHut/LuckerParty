using LuckerParty.Core;

void Require(bool condition,string message)
{ if (!condition) throw new Exception(message); Console.WriteLine("CORE_CHECK_PASS: "+message); }
void Reject(byte[] packet)
{ try { InputCodec.Decode(packet); throw new Exception("Invalid packet was accepted"); } catch (InvalidDataException) { } }
var jump=new NetworkInput(42,.7f,-.7f,1,-.5f,true,true,false);
Require(InputCodec.Decode(InputCodec.Encode(new[]{jump})).Single()==jump,"Input flags and angles survive the wire format");
Reject(null!);Reject(Array.Empty<byte>());Reject(new byte[]{17});Reject(new byte[]{1,0,0});
Reject(InputCodec.Encode(new[]{jump with {X=float.NaN}}));
Reject(InputCodec.Encode(new[]{jump with {Yaw=float.PositiveInfinity}}));
Reject(InputCodec.Encode(new[]{jump with {X=9}}));
Reject(InputCodec.Encode(new[]{jump,jump}));
var unknown=InputCodec.Encode(new[]{jump});unknown[^1]=255;Reject(unknown);
Require(InputCodec.Decode(InputCodec.Encode(Enumerable.Range(1,40).Select(i=>jump with {Sequence=i}))).Length==32,"Movement redundancy is bounded");
Require(PlayerNames.Clean("\nAlice\u0000🙂\t")=="Alice","Names discard controls and unsupported glyphs");
Require(PlayerNames.Clean("Élodie 東京")=="Élodie 東京","Names retain international letters");
Require(PlayerNames.Clean("   ")=="Player","Empty names have a usable fallback");
Require(PlayerNames.Clean(new string('A',100)).Length==24,"Names have a bounded display length");
Console.WriteLine("CORE_NETWORK_CHECK_PASS: malformed, nonfinite, repeated inputs and display name rules");
PartyChecks.Run(Require);
