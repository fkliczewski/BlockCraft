import java.util.*;
import java.util.jar.*;
import java.lang.reflect.*;
public class EnchOracle {
 static Method build, cost; static Field edEnch, edLvl, enchId;
 static String fmt(List<?> l) throws Exception {
  if (l == null) return "null"; StringBuilder sb = new StringBuilder();
  for (Object o : l) { if (sb.length() > 0) sb.append(','); sb.append(one(o)); }
  return sb.toString();
 }
 static String one(Object o) throws Exception {
  if (edEnch == null) { for (Field f : o.getClass().getDeclaredFields()) { if (Modifier.isStatic(f.getModifiers())) continue; f.setAccessible(true); if (f.getType() == int.class) edLvl = f; else edEnch = f; }
   for (Field f : edEnch.getType().getDeclaredFields()) if (!Modifier.isStatic(f.getModifiers()) && Modifier.isPublic(f.getModifiers()) && f.getType() == int.class) enchId = f; } return enchId.getInt(edEnch.get(o)) + ":" + edLvl.getInt(o); }
 public static void main(String[] a) throws Exception {
  Class.forName("kb").getMethod("c").invoke(null);
  Class<?> helper = null;
  try (JarFile jar = new JarFile(a[0])) {
   for (JarEntry e : Collections.list(jar.entries())) {
    String n = e.getName(); if (!n.endsWith(".class") || n.contains("/")) continue;
    Class<?> c; try { c = Class.forName(n.replace(".class", ""), false, EnchOracle.class.getClassLoader()); } catch (Throwable t) { continue; }
    Method b = null, k = null;
    for (Method m : c.getDeclaredMethods()) {
     if (!Modifier.isStatic(m.getModifiers())) continue; Class<?>[] p = m.getParameterTypes();
     if (p.length == 3 && p[0] == Random.class && p[2] == int.class && m.getReturnType() == List.class) b = m;
     if (p.length == 4 && p[0] == Random.class && p[1] == int.class && p[2] == int.class && m.getReturnType() == int.class) k = m;
    }
    if (b != null && k != null && k.getParameterTypes()[3] == b.getParameterTypes()[1]) { helper = c; build = b; cost = k; }
   }
  }
  Class<?> stackC = build.getParameterTypes()[1];
  Constructor<?>[] ctors = stackC.getConstructors();
  Class<?> itemC = null;
  for (Method m : stackC.getMethods()) if (m.getParameterCount() == 0) for (Constructor<?> ct : ctors)
   if (ct.getParameterCount() == 3 && ct.getParameterTypes()[0] == m.getReturnType() && ct.getParameterTypes()[1] == int.class) itemC = m.getReturnType();
  Constructor<?> mk = stackC.getConstructor(itemC, int.class, int.class);
  Method byId = null;
  for (Method m : itemC.getDeclaredMethods()) if (Modifier.isStatic(m.getModifiers()) && m.getReturnType() == itemC && m.getParameterCount() == 1 && m.getParameterTypes()[0] == int.class) byId = m;
  System.err.println("helper=" + helper.getName() + " stack=" + stackC.getName() + " item=" + itemC.getName());
  int[] items = {268,269,270,271,272,273,274,275,267,256,257,258,276,277,278,279,283,284,285,286,293,
   298,299,300,301,302,303,304,305,306,307,308,309,310,311,312,313,314,315,316,317,261,346,340,359,280};
  int[] seeds = {0, 1, -1, 7, 12345, -987654321, 2147483647, -2147483648, 99, 31337};
  Random r = new Random();
  for (int id : items) {
   Object item = byId.invoke(null, id);
   for (int shelves = 0; shelves <= 15; shelves++) for (int seed : seeds) {
    Object st = mk.newInstance(item, 1, 0);
    boolean book = id == 340;
    r.setSeed(seed); int[] c = new int[3];
    for (int i = 0; i < 3; i++) { c[i] = (int) cost.invoke(null, r, i, shelves, st); if (c[i] < i + 1) c[i] = 0; }
    String[] ls = new String[3], cl = new String[3];
    for (int i = 0; i < 3; i++) {
     ls[i] = "-"; cl[i] = "-";
     if (c[i] > 0) {
      r.setSeed((long)(seed + i));
      List<?> l = (List<?>) build.invoke(null, r, st, c[i]);
      if (book && l != null && l.size() > 1) l.remove(r.nextInt(l.size()));
      ls[i] = fmt(l);
      if (l != null && !l.isEmpty()) cl[i] = one(l.get(r.nextInt(l.size())));
     }
    }
    System.out.println("T " + id + " " + shelves + " " + seed + " " + c[0] + " " + c[1] + " " + c[2] + " | " + ls[0] + " ; " + ls[1] + " ; " + ls[2] + " | " + cl[0] + " " + cl[1] + " " + cl[2]);
   }
   for (int power = 1; power <= 60; power += 3) for (int seed : seeds) {
    Object st = mk.newInstance(item, 1, 0);
    r.setSeed(seed);
    System.out.println("B " + id + " " + power + " " + seed + " " + fmt((List<?>) build.invoke(null, r, st, power)));
   }
  }
 }
}
