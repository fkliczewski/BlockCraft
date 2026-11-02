import java.lang.reflect.*;
import java.util.*;

// Uruchamiany z oficjalnym server.jar 1.8.9, bez uruchamiania serwera i zapisu świata.
public class StructureOracle {
 static class Dummy extends TerrainOracle.Dummy {
  Dummy() { super(); }
  public int F() { return 63; }
 }
 static Random mapRandom(long seed,int x,int z) {
  Random r=new Random(seed);long a=r.nextLong(),b=r.nextLong();
  r.setSeed(x*a^z*b^seed);r.nextInt();return r;
 }
 static long hash(List<aqt> pieces) {
  long h=0xcbf29ce484222325L;
  for(aqt p:pieces) {
   aqe b=p.c();
   for(int v:new int[]{b.a,b.b,b.c,b.d,b.e,b.f,p.m==null?-1:p.m.b(),p.n}) h=(h^(long)v)*0x100000001b3L;
  }
  return h;
 }
 public static void main(String[] args) throws Exception {
  kb.c();Field f=sun.misc.Unsafe.class.getDeclaredField("theUnsafe");f.setAccessible(true);
  sun.misc.Unsafe u=(sun.misc.Unsafe)f.get(null);
  for(long seed:new long[]{0,1,12345,-987654321012345L}) {
   Dummy w=(Dummy)u.allocateInstance(Dummy.class);
   adr type=adr.a("default");w.cm=new aec(seed,type,"");w.info=new ato(new adp(seed,adp.a(0),false,false,type),"ref");w.x=w.info;
   aqo gen=new aqo();gen.c=w;gen.a(0,0);
   Field coords=aqo.class.getDeclaredField("g");coords.setAccessible(true);
   for(adg p:(adg[])coords.get(gen)) System.out.printf("P %d %d %d%n",seed,p.a,p.b);
   for(int[] pos:new int[][]{{0,0},{20,-10},{-55,49}}) {
    int x=pos[0],z=pos[1];gen.b=mapRandom(seed,x,z);aqu sh=gen.b(x,z);
    System.out.printf("S %d %d %d %d %016x%n",seed,x,z,sh.b().size(),hash(sh.b()));
    aqv.a village=new aqv.a(w,mapRandom(seed,x,z),x,z,0);
    System.out.printf("V %d %d %d %d %016x%n",seed,x,z,village.b().size(),hash(village.b()));
    aqk.a monument=new aqk.a(w,mapRandom(seed,x,z),x,z);
    aqt building=monument.b().getFirst();Field rooms=building.getClass().getDeclaredField("q");rooms.setAccessible(true);
    List<aqt> all=new ArrayList<aqt>();all.add(building);all.addAll((List<aqt>)rooms.get(building));
    System.out.printf("M %d %d %d %d %016x%n",seed,x,z,all.size(),hash(all));
   }
   int count=0;
   for(int rx=-5;rx<=5&&count<3;rx++) for(int rz=-5;rz<=5&&count<3;rz++) {
    Random r=new Random(rx*341873128712L+rz*132897987541L+seed+10387312);
    int x=rx*32+r.nextInt(24),z=rz*32+r.nextInt(24);
    if(!w.cm.a(x*16+8,z*16+8,0,aqv.d)) continue;
    aqv.a village=new aqv.a(w,mapRandom(seed,x,z),x,z,0);
    System.out.printf("V %d %d %d %d %016x%n",seed,x,z,village.b().size(),hash(village.b()));count++;
   }
  }
 }
}
