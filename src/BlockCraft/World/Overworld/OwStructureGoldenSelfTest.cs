using BlockCraft.World;
using BlockCraft.World.Structures18;
namespace BlockCraft;
public static partial class SelfTest
{
 private static void TestOwStructureGolden()
 {
  // Wzorce z tools/overworld/StructureOracle.java i oficjalnego server.jar 1.8.9.
  const string golden = """
P 0 -9 -41
P 0 67 32
P 0 -37 39
S 0 0 0 182 2462c6469bdd6017
V 0 0 0 1 587c4eaad30368d4
M 0 0 0 32 9310294c88f3c854
S 0 20 -10 167 5afc9f15c25f9cb2
V 0 20 -10 1 a231709b10f1ee94
M 0 20 -10 34 834c8abafe660d54
S 0 -55 49 119 7147f11a639bfcec
V 0 -55 49 1 6c3cecfbb261fb9d
M 0 -55 49 34 9d5479a6d21fcdd8
V 0 -156 -94 20 65e452c8960a5524
V 0 -145 -58 17 ffd894ed859f7167
V 0 -127 -78 20 6723f9feaf3a91fa
P 1 -6 -46
P 1 63 32
P 1 -57 49
S 1 0 0 160 eaa7158769958071
V 1 0 0 1 587fb4aad3064bfd
M 1 0 0 31 d69d0f0b6bfeca74
S 1 20 -10 138 8ac6a9cd77ffc48b
V 1 20 -10 1 a2383c9b10f7b4e6
M 1 20 -10 32 7a5dce349111c91c
S 1 -55 49 21 0785e25250d1cbca
V 1 -55 49 1 6c3cecfbb261fb9d
M 1 -55 49 32 ca3db211c035715c
V 1 -145 -106 6 3f9b43a409452029
V 1 -159 -58 1 b68d31c7a77d4ef4
V 1 -115 -108 29 77908cd599d5cf89
P 12345 -44 59
P 12345 -16 -38
P 12345 61 18
S 12345 0 0 239 003e096292a74afb
V 12345 0 0 1 587c4eaad30368d4
M 12345 0 0 31 f0e49b054e537433
S 12345 20 -10 35 16bcb92473180c7b
V 12345 20 -10 1 a234d69b10f4d1bd
M 12345 20 -10 30 07915216e7ab1bb8
S 12345 -55 49 136 c06b06988e65db0d
V 12345 -55 49 1 6c43b8fbb267c1ef
M 12345 -55 49 33 3c357efce534cbf7
V 12345 -108 -50 24 df42e34c9e48a6b8
V 12345 -109 22 1 41bbc31ba69b6ff4
V 12345 -128 118 26 082fe0fe30959d07
P -987654321012345 -8 -44
P -987654321012345 51 9
P -987654321012345 -37 41
S -987654321012345 0 0 158 876a5ac17f5117cd
V -987654321012345 0 0 1 587c4eaad30368d4
M -987654321012345 0 0 31 32a75eb9ddacd44c
S -987654321012345 20 -10 259 8a2a2329f0193bde
V -987654321012345 20 -10 1 a23ba29b10fa980f
M -987654321012345 20 -10 33 ca08b143e3080190
S -987654321012345 -55 49 27 cd1fd73ed0a043b8
V -987654321012345 -55 49 1 6c43b8fbb267c1ef
M -987654321012345 -55 49 31 5c8d7837471221a8
V -987654321012345 -148 -127 1 2a083c7293c4d9bd
V -987654321012345 -140 87 4 e3a4af485b88fc09
V -987654321012345 -154 146 20 eddabcfc43cf999e
""";
  int failures=0, positions=0, villages=0, strongholds=0, monuments=0;
  var generators=new Dictionary<long,OwGenerator>();
  foreach(string line in golden.Split('\n',StringSplitOptions.RemoveEmptyEntries))
  {
   var t=line.Trim().Split(' ');long seed=long.Parse(t[1]);int x=int.Parse(t[2]),z=int.Parse(t[3]);
   if(!generators.TryGetValue(seed,out var gen)) generators[seed]=gen=new OwGenerator(seed,WorldType.Default);
   if(t[0]=="P") {positions++;if(!gen.StrongholdChunks().Contains((x,z))) failures++;continue;}
   var r=gen.MapRandom(x,z);JavaList<StructureComponent> pieces;
   if(t[0]=="S") {strongholds++;pieces=gen.StrongholdComponents(r,x,z);}
   else if(t[0]=="V") {villages++;pieces=gen.VillageComponents(r,x,z);}
   else
   {
    monuments++;var seedRandom=new JRandom(seed);long a=seedRandom.NextLong(),b=seedRandom.NextLong();r=new JRandom(unchecked(x*a^z*b^seed));
    var building=new StructureOceanMonumentPieces.MonumentBuilding(r,x*16-21,z*16-21,Facing.RandomHorizontal(r));
    pieces=new JavaList<StructureComponent>{building};pieces.AddRange(building.field_175843_q);
   }
   if(pieces.Count!=int.Parse(t[4])||OwComponentHash(pieces)!=ulong.Parse(t[5],System.Globalization.NumberStyles.HexNumber)) failures++;
  }
  Check(failures==0,$"Struktury: {positions} pozycji twierdz i grafy {strongholds} twierdz, {villages} wiosek, {monuments} monumentów zgodne z MC 1.8.9",failures.ToString());
 }
}
