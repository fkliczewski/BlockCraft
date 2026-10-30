import java.util.*;
import java.util.jar.*;
import java.lang.reflect.*;
import java.nio.*;
public class GenLayerOracle {
 public static void main(String[] args) throws Exception {
  Class.forName("kb").getMethod("c").invoke(null); String layerName=null;
  try(JarFile jar = new JarFile(args[0])) {
   for (JarEntry e: Collections.list(jar.entries())) {
    if(!e.getName().endsWith(".class") || e.getName().contains("/")) continue;
    byte[] b = jar.getInputStream(e).readAllBytes();
    for(int i=0;i<b.length-8;i++) if(ByteBuffer.wrap(b,i,8).getLong()==6364136223846793005L) {
     Class<?> c=Class.forName(e.getName().replace(".class",""),false,GenLayerOracle.class.getClassLoader());
     for(Method m:c.getDeclaredMethods()) if(Modifier.isStatic(m.getModifiers()) && m.getParameterCount()==3 && m.getParameterTypes()[0]==long.class && m.getReturnType().isArray() && m.getReturnType().getComponentType()==c) layerName=c.getName();
    }
   }
  }
  Class<?> layer=Class.forName(layerName); Method build=null,get=null;
  for(Method m:layer.getDeclaredMethods()) {
   if(Modifier.isStatic(m.getModifiers()) && m.getParameterCount()==3 && m.getParameterTypes()[0]==long.class && m.getReturnType().isArray()) build=m;
   if(m.getReturnType()==int[].class && m.getParameterCount()==4) get=m;
  }
  Class<?> type=build.getParameterTypes()[1];
  Object def=null,large=null;
  for(Field f:type.getDeclaredFields()) if(Modifier.isStatic(f.getModifiers()) && f.getType()==type) {
   Object v=f.get(null);
   for(Field sf:type.getDeclaredFields()) if(sf.getType()==String.class && !Modifier.isStatic(sf.getModifiers())) {
    sf.setAccessible(true); Object s=sf.get(v);
    if("default".equals(s)) def=v;
    if("largeBiomes".equals(s)) large=v;
   }
  }
  for(long seed:new long[]{0,1,12345,-987654321012345L}) for(boolean big:new boolean[]{false,true}) {
   Object[] layers=(Object[])build.invoke(null,seed,big?large:def,"");
   for(int idx=0;idx<2;idx++) for(int[] box:new int[][]{{-32,-32,64,64},{137,-231,32,24},{-10000,2345,24,32}}) {
    int[] a=(int[])get.invoke(layers[idx],box[0],box[1],box[2],box[3]);
    long hash=0xcbf29ce484222325L;
    for(int j=0;j<box[2]*box[3];j++) hash=(hash^a[j])*0x100000001b3L;
    System.out.printf("%d %s %d %d %d %d %d %016x%n",seed,big,idx,box[0],box[1],box[2],box[3],hash);
   }
  }
 }
}
