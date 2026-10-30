import java.lang.reflect.*;
import java.util.*;
public class TerrainOracle {
 static class Dummy extends adm {
  aec cm; ato info;
  Dummy() { super(null,null,null,null,false); }
  protected amv k() { return null; }
  protected int q() { return 0; }
  public aec v() { return cm; }
  public ato P() { return info; }
 }
 public static void main(String[] args) throws Exception {
  kb.c();
  Field uf=sun.misc.Unsafe.class.getDeclaredField("theUnsafe"); uf.setAccessible(true);
  sun.misc.Unsafe u=(sun.misc.Unsafe)uf.get(null);
  for(long seed:new long[]{0,1,12345,-987654321012345L}) for(String name:new String[]{"default","largeBiomes","amplified"}) {
   adr type=adr.a(name);
   Dummy world=(Dummy)u.allocateInstance(Dummy.class);
   world.cm=new aec(seed,type,"");
   world.info=new ato(new adp(seed,adp.a(0),false,false,type),"ref");
   world.x=world.info; aoa gen=new aoa(world,seed,false,"");
   Field rf=aoa.class.getDeclaredField("h");rf.setAccessible(true);Random r=(Random)rf.get(gen);
   for(int[] pos:new int[][]{{0,0},{-3,7},{137,-231},{-625,146}}) {
    int x=pos[0],z=pos[1];
    r.setSeed(x*341873128712L+z*132897987541L);
    ans primer=new ans(); gen.a(x,z,primer);
    ady[] biomes=world.cm.b(null,x*16,z*16,16,16);
    gen.a(x,z,primer,biomes);
    long hash=0xcbf29ce484222325L;
    for(int y=0;y<256;y++) for(int zz=0;zz<16;zz++) for(int xx=0;xx<16;xx++) hash=(hash^afh.f(primer.a(xx,y,zz)))*0x100000001b3L;
    System.out.printf("%d %s %d %d %016x%n",seed,name,x,z,hash);
    if(args.length>0) {
     java.io.DataOutputStream out=new java.io.DataOutputStream(new java.io.FileOutputStream(args[0]+"/terrain-"+seed+"-"+name+"-"+x+"-"+z+".bin"));
     for(int y=0;y<256;y++) for(int zz=0;zz<16;zz++) for(int xx=0;xx<16;xx++) out.writeShort(afh.f(primer.a(xx,y,zz)));
     out.close();
    }
   }
  }
 }
}
