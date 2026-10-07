import struct, zlib, glob
M = 0xFFFFFFFFFFFFFFFF
k0=0xc3a5c85c97cb3127; k1=0xb492b66fbe98f273; k2=0x9ae16a3b2f90404f
def f64(s,i): return struct.unpack_from("<Q",s,i)[0]
def f32(s,i): return struct.unpack_from("<I",s,i)[0]
def rot(v,s): return v if s==0 else ((v>>s)|(v<<(64-s)))&M
def smix(v): return v^(v>>47)
def h16(u,v,mul=0x9ddfea08eb382d69):
    a=((u^v)*mul)&M; a^=a>>47; b=((v^a)*mul)&M; b^=b>>47; return (b*mul)&M
def h0to16(s,n):
    if n>=8:
        mul=(k2+n*2)&M; a=(f64(s,0)+k2)&M; b=f64(s,n-8)
        c=(rot(b,37)*mul+a)&M; d=((rot(a,25)+b)*mul)&M; return h16(c,d,mul)
    if n>=4:
        mul=(k2+n*2)&M; a=f32(s,0); return h16((n+(a<<3))&M, f32(s,n-4), mul)
    if n>0:
        a=s[0]; b=s[n>>1]; c=s[n-1]; y=(a+(b<<8))&0xFFFFFFFF; z=(n+(c<<2))&0xFFFFFFFF
        return (smix(((y*k2)^(z*k0))&M)*k2)&M
    return k2
def h17to32(s,n):
    mul=(k2+n*2)&M; a=(f64(s,0)*k1)&M; b=f64(s,8); c=(f64(s,n-8)*mul)&M; d=(f64(s,n-16)*k2)&M
    return h16((rot((a+b)&M,43)+rot(c,30)+d)&M, (a+rot((b+k2)&M,18)+c)&M, mul)
def wh(w,x,y,z,a,b):
    a=(a+w)&M; b=rot((b+a+z)&M,21); c=a; a=(a+x)&M; a=(a+y)&M; b=(b+rot(a,44))&M
    return (a+z)&M,(b+c)&M
def whs(s,i,a,b): return wh(f64(s,i),f64(s,i+8),f64(s,i+16),f64(s,i+24),a,b)
def h33to64(s,n):
    mul=(k2+n*2)&M; a=(f64(s,0)*k2)&M; b=f64(s,8); c=f64(s,n-24); d=f64(s,n-32); e=(f64(s,16)*k2)&M; f=(f64(s,24)*9)&M; g=f64(s,n-8); h=(f64(s,n-16)*mul)&M
    u=(rot((a+g)&M,43)+(rot(b,30)+c)*9)&M; v=(((a+g)^d)+f+1)&M; w=((((u+v)*mul)&M)>>0)
    w=(((u+v)*mul)&M); w=((w>>0)); 
    # bswap
    def bs(x): return int.from_bytes(x.to_bytes(8,"little"),"big")
    w=(bs(((u+v)*mul)&M)+h)&M; x=(rot((e+f)&M,42)+c)&M; y=((bs(((v+w)*mul)&M)+g)*mul)&M; z=(e+f+c)&M
    a=(bs(((x+z)*mul+y)&M)+b)&M; b=(smix(((z+a)*mul+d+h)&M)*mul)&M; return (b+x)&M
def city64(s):
    n=len(s)
    if n<=32: return h0to16(s,n) if n<=16 else h17to32(s,n)
    if n<=64: return h33to64(s,n)
    x=f64(s,n-40); y=(f64(s,n-16)+f64(s,n-56))&M; z=h16((f64(s,n-48)+n)&M, f64(s,n-24))
    v=whs(s,n-64,n,z); w=whs(s,n-32,(y+k1)&M,x); x=(x*k1+f64(s,0))&M
    n2=(n-1)&~63; i=0
    while True:
        x=(rot((x+y+v[0]+f64(s,i+8))&M,37)*k1)&M; y=(rot((y+v[1]+f64(s,i+48))&M,42)*k1)&M
        x^=w[1]; y=(y+v[0]+f64(s,i+40))&M; z=(rot((z+w[0])&M,33)*k1)&M
        v=whs(s,i,(v[1]*k1)&M,(x+w[0])&M); w=whs(s,i+32,(z+w[1])&M,(y+f64(s,i+16))&M)
        z,x=x,z; i+=64; n2-=64
        if n2==0: break
    return h16((h16(v[0],w[0])+smix(y)*k1+z)&M, (h16(v[1],w[1])+x)&M)
def keyhash(t):
    h=city64(t.encode("utf-16le")); return ((h&0xFFFFFFFF)+((h>>32)*23))&0xFFFFFFFF
def srchash(t): return zlib.crc32(t.encode("utf-32le"))
print("srchash test", srchash("Hit the corpse with a tool to harvest, press {0} to loot or hold {0} for more options"), "expect 3284001753")
# key hash test against stored file
d=open(glob.glob(r"C:\Users\vkkha\AppData\Local\Temp\claude\E--github-gameee\38dc645d-3cc9-4427-a274-ffaba69e9f7f\scratchpad\vihoa\**\Exiles_UI.locres",recursive=True)[0],"rb").read()
p=17+8+4; nsc=struct.unpack_from("<I",d,p)[0]; p+=4
def rstr(p):
    n=struct.unpack_from("<i",d,p)[0]; p+=4
    if n>=0: return (d[p:p+n-1].decode("latin1") if n else ""), p+n
    return d[p:p-2*n-2].decode("utf-16le"), p-2*n
ok=bad=0
for _ in range(nsc):
    nh=struct.unpack_from("<I",d,p)[0]; p+=4; ns,p=rstr(p)
    if keyhash(ns)==nh: ok+=1
    else: bad+=1; print("ns mismatch", repr(ns), nh, keyhash(ns))
    kc=struct.unpack_from("<I",d,p)[0]; p+=4
    for j in range(kc):
        kh=struct.unpack_from("<I",d,p)[0]; p+=4; k,p=rstr(p); p+=8
        if keyhash(k)==kh: ok+=1
        else:
            bad+=1
            if bad<5: print("key mismatch", repr(k), kh, keyhash(k))
print("ok",ok,"bad",bad)
