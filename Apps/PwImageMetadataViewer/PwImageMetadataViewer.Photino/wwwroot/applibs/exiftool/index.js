var $e=((e)=>typeof require<"u"?require:typeof Proxy<"u"?new Proxy(e,{get:(a,r)=>(typeof require<"u"?require:a)[r]}):e)(function(e){if(typeof require<"u")return require.apply(this,arguments);throw Error('Dynamic require of "'+e+'" is not supported')});class i{static WASI_ESUCCESS=0;static WASI_ERRNO_BADF=8;static WASI_ENOSYS=52;static WASI_CLOCK_REALTIME=0;static WASI_CLOCK_MONOTONIC=1;static WASI_ERRNO_ISDIR=31;static WASI_ERRNO_INVAL=28;static WASI_ERRNO_NOTDIR=54;static WASI_ERRNO_NOENT=44;static WASI_ERRNO_EXIST=20;static WASI_ERRNO_IO=29;static WASI_FILETYPE_CHARACTER_DEVICE=2;static WASI_FILETYPE_DIRECTORY=3;static WASI_FILETYPE_REGULAR_FILE=4;static IMPORT_FUNCTIONS=["args_get","args_sizes_get","clock_res_get","clock_time_get","environ_get","environ_sizes_get","fd_advise","fd_allocate","fd_close","fd_datasync","fd_fdstat_get","fd_fdstat_set_flags","fd_fdstat_set_rights","fd_filestat_get","fd_filestat_set_size","fd_filestat_set_times","fd_pread","fd_prestat_dir_name","fd_prestat_get","fd_pwrite","fd_read","fd_readdir","fd_renumber","fd_seek","fd_sync","fd_tell","fd_write","path_create_directory","path_filestat_get","path_filestat_set_times","path_link","path_open","path_readlink","path_remove_directory","path_rename","path_symlink","path_unlink_file","poll_oneoff","proc_exit","proc_raise","random_get","sched_yield","sock_accept","sock_recv","sock_send","sock_shutdown"];encoder;decoder;constructor(){this.encoder=new TextEncoder,this.decoder=new TextDecoder}stringArraySize(e){let a=e.length*4,r=e.reduce((t,n)=>t+this.byteLength(n)+1,0);return{pointerArraySize:a,bufferSize:r,totalSize:a+r}}writeStringArray(e,a,r,t){let n=r,o=t;for(let s of a)e.setUint32(n,o,!0),n+=4,o+=this.writeString(e,`${s}\x00`,o);return o-t}writeString(e,a,r){let t=this.encoder.encode(a);return new Uint8Array(e.buffer,r,t.length).set(t),t.length}readString(e,a,r){let t=new Uint8Array(e.buffer,a,r);return this.decoder.decode(t)}byteLength(e){return this.encoder.encode(e).length}static iovec_t={size:8,bufferOffset:0,lengthOffset:4};iovViews(e,a,r){let t=[],n=a;for(let o=0;o<r;o++){let s=e.getUint32(n+i.iovec_t.bufferOffset,!0),l=e.getUint32(n+i.iovec_t.lengthOffset,!0);t.push(new Uint8Array(e.buffer,s,l)),n+=i.iovec_t.size}return t}writeFilestat(e,a,r,t=0n,n=0n,o=0n,s=0n){e.setBigUint64(a,0n,!0),e.setBigUint64(a+8,0n,!0),e.setUint8(a+16,r),e.setBigUint64(a+24,1n,!0),e.setBigUint64(a+32,t,!0),e.setBigUint64(a+40,n,!0),e.setBigUint64(a+48,o,!0),e.setBigUint64(a+56,s,!0)}writeFdstat(e,a,r,t,n,o){e.setUint8(a,r),e.setUint16(a+2,t,!0),e.setBigUint64(a+8,n,!0),e.setBigUint64(a+16,o,!0)}}class v{code;constructor(e){this.code=e}get exitCode(){return this.code}}function Y(e,a,r){let t=e.args||[];return{args_get:(n,o)=>{let s=r();return a.writeStringArray(s,t,n,o),i.WASI_ESUCCESS},args_sizes_get:(n,o)=>{let s=r();s.setUint32(n,t.length,!0);let l=a.stringArraySize(t);return s.setUint32(o,l.bufferSize,!0),i.WASI_ESUCCESS}}}function J(e,a,r){return{clock_res_get:(t,n)=>{let o;switch(t){case i.WASI_CLOCK_MONOTONIC:{o=5000;break}case i.WASI_CLOCK_REALTIME:{o=1000;break}default:return i.WASI_ENOSYS}return r().setUint32(n,o,!0),i.WASI_ESUCCESS},clock_time_get:(t,n,o)=>{let s=0;switch(t){case i.WASI_CLOCK_MONOTONIC:{s=performance.now();break}case i.WASI_CLOCK_REALTIME:{s=Date.now();break}default:return i.WASI_ENOSYS}let l=r();if(BigInt){let m=BigInt(((S)=>{let A=Math.trunc(S),$=BigInt(Math.round((S-A)*1e6));return BigInt(A)*BigInt(1e6)+$})(s));l.setBigUint64(o,m,!0)}else{let c=Date.now()*1e6;l.setUint32(o,c&65535,!0),l.setUint32(o+4,c&4294901760,!0)}return i.WASI_ESUCCESS}}}function Z(e,a,r){return{environ_get:(t,n)=>{let o=t,s=n,l=r();for(let c in e.env){let m=e.env[c];l.setUint32(o,s,!0),o+=4,s+=a.writeString(l,`${c}=${m}\x00`,s)}return i.WASI_ESUCCESS},environ_sizes_get:(t,n)=>{let o=r();return o.setUint32(t,Object.keys(e.env||{}).length,!0),o.setUint32(n,Object.entries(e.env||{}).reduce((s,[l,c])=>{return s+a.byteLength(l)+1+a.byteLength(c)+1},0),!0),i.WASI_ESUCCESS}}}class w{handler;outputBuffers;decoder=new TextDecoder("utf-8");constructor(e,a){this.handler=e;this.outputBuffers=a}writev(e){let a=e.reduce((n,o)=>n+o.byteLength,0),r=0,t=new Uint8Array(a);for(let n of e)t.set(n,r),r+=n.byteLength;if(this.outputBuffers)this.handler(t);else{let n=this.decoder.decode(t);this.handler(n)}return t.length}readv(e){return 0}close(){}}class ee{consume;encoder=new TextEncoder;pending=null;constructor(e){this.consume=e}writev(e){return 0}consumePending(e,a){if(e.byteLength<a)return this.pending=null,e;let r=e.slice(0,a);return this.pending=e.slice(a),r}readv(e){let a=0;for(let r of e){let t=r.byteLength;if(this.pending){let n=this.consumePending(this.pending,t);r.set(n,0),t-=n.byteLength,a+=n.byteLength}while(t>0){let n=this.consume(),o;if(n instanceof Uint8Array)o=n;else o=this.encoder.encode(n);if(o.length===0)return a;if(o.length>t)r.set(o.slice(0,t),r.byteLength-t),this.pending=o.slice(t),a+=t,t=0;else r.set(o,r.byteLength-t),a+=o.length,t-=o.length}}return a}close(){}}function ue(e={}){let a=e.outputBuffers||!1;return[new ee(e.stdin||(()=>{return""})),new w(e.stdout||console.log,a),new w(e.stderr||console.error,a)]}class I{root;preopenPaths=[];constructor(e){if(this.root={type:"dir",entries:{}},this.ensureDir("/dev"),this.setNode("/dev/null",{type:"character",kind:"devnull"}),e)for(let a of Object.keys(e))this.ensureDir(a),this.preopenPaths.push(a);else this.preopenPaths.push("/")}removeFile(e){let r=this.normalizePath(e).split("/").filter((s)=>s.length>0),t=r.pop(),n=`/${r.join("/")}`,o=this.ensureDir(n);if(t)delete o.entries[t]}addFile(e,a){if(typeof a==="string"){let r=new TextEncoder().encode(a);this.createFile(e,r);return}this.createFile(e,a)}createFile(e,a){let r={type:"file",content:a};return this.setNode(e,r),r}setNode(e,a){let t=this.normalizePath(e).split("/").filter((l)=>l.length>0);if(t.length===0){if(a.type!=="dir")throw Error("Root must be a directory");this.root=a;return}let n=t.pop(),o=`/${t.join("/")}`,s=this.ensureDir(o);if(n)s.entries[n]=a}getDevNull(){let e=this.lookup("/dev/null");if(!e)throw Error("/dev/null not found");return e}getPreopenPaths(){return[...this.preopenPaths]}lookup(e){let a=this.normalizePath(e);if(a==="/")return this.root;let r=a.split("/").filter((n)=>n.length>0),t=this.root;for(let n of r){if(t.type!=="dir")return null;if(t=t.entries[n],!t)return null}return t}resolve(e,a){let t=this.normalizePath(a).split("/").filter((o)=>o.length>0),n=e;for(let o of t){if(o===".")continue;if(o===".."){n=this.root;continue}if(n.type!=="dir")return null;if(n=n.entries[o],!n)return null}return n}ensureDir(e){let r=this.normalizePath(e).split("/").filter((n)=>n.length>0),t=this.root;for(let n of r){if(!t.entries[n])t.entries[n]={type:"dir",entries:{}};let o=t.entries[n];if(o.type!=="dir")throw Error(`"${n}" is not a directory`);t=o}return t}createFileIn(e,a){let t=this.normalizePath(a).split("/").filter((l)=>l.length>0);if(t.length===0)throw Error("Cannot create a file with an empty name");let n=t.pop();if(!n)throw Error("Cannot create a file with an empty name");let o=e;for(let l of t){if(!o.entries[l])o.entries[l]={type:"dir",entries:{}};let c=o.entries[l];if(c.type!=="dir")throw Error(`"${l}" is not a directory`);o=c}let s={type:"file",content:new Uint8Array(0)};return o.entries[n]=s,s}normalizePath(e){if(!e)return"/";let r=(e.startsWith("/")?e:`/${e}`).replace(/\/+/g,"/");return r==="/"?r:r.replace(/\/+$/,"")}}function q(e={}){return(a,r,t)=>{let n=e.withFileSystem||new I(a.preopens),o={};function s(A){if(A.type==="file"&&A.content instanceof Blob){let $=BigInt(A.content.lastModified??Date.now())*1000000n;return{atim:$,mtim:$,ctim:$}}return{atim:0n,mtim:0n,ctim:0n}}ue(e.withStdIo||{}).forEach((A,$)=>{o[$]={node:{type:"character",kind:"stdio",entry:A},position:0,isPreopen:!1,path:`/dev/fd/${$}`,fd:$}});let l=3;for(let A of n.getPreopenPaths()){let $=n.lookup(A);if($&&$.type==="dir")o[l]={node:$,position:0,isPreopen:!0,preopenPath:A,path:A,fd:l},l++}function c(A){for(let $ in o){let f=o[$];if(f?.path===A)return f}return null}function m(A){return o[A]||null}function S(A){if(!A||!A.content)return 0;if(A.content instanceof Blob)return A.content.size;return A.content.byteLength||0}return{fd_read:async(A,$,f,d)=>{let E=t(),p=r.iovViews(E,$,f),u=m(A);if(!u)return i.WASI_ERRNO_BADF;if(u.node.type==="character"&&u.node.kind==="stdio"){let h=u.node.entry.readv(p);return E.setUint32(d,h,!0),i.WASI_ESUCCESS}if(u.node.type==="dir")return i.WASI_ERRNO_ISDIR;if(u.node.type==="character"&&u.node.kind==="devnull")return E.setUint32(d,0,!0),i.WASI_ESUCCESS;let g=u.node,y=g.content,k=S(g)-u.position,C=0;if(k<=0)return E.setUint32(d,0,!0),i.WASI_ESUCCESS;if(g.content instanceof Blob){let h=g.content;for(let b of p){if(u.position>=h.size)break;let x=Math.min(b.byteLength,h.size-u.position);if(x<=0)break;let T=await h.slice(u.position,u.position+x).arrayBuffer();b.set(new Uint8Array(T)),C+=T.byteLength,u.position+=T.byteLength}}else if(ArrayBuffer.isView(y))for(let h of p){if(u.position>=y.byteLength)break;let b=Math.min(h.byteLength,y.byteLength-u.position);if(b<=0)break;h.set(y.slice(u.position,u.position+b)),C+=b,u.position+=b}return E.setUint32(d,C,!0),i.WASI_ESUCCESS},fd_write:(A,$,f,d)=>{let E=t(),p=r.iovViews(E,$,f),u=m(A);if(!u)return i.WASI_ERRNO_BADF;let g=0;if(u.node.type==="character"&&u.node.kind==="stdio"){let b=u.node.entry.writev(p);return E.setUint32(d,b,!0),i.WASI_ESUCCESS}if(u.node.type==="dir")return i.WASI_ERRNO_ISDIR;if(u.node.type==="character"&&u.node.kind==="devnull"){let b=p.reduce((x,T)=>x+T.byteLength,0);return E.setUint32(d,b,!0),i.WASI_ESUCCESS}if(u.node.content instanceof Blob)return i.WASI_ERRNO_INVAL;let y=u.position,k=p.reduce((b,x)=>b+x.byteLength,0),C=y+k,h;if(C>S(u.node))h=new Uint8Array(C),h.set(u.node.content,0);else h=u.node.content;for(let b of p)h.set(b,y),y+=b.byteLength,g+=b.byteLength;return u.node.content=h,u.position=y,E.setUint32(d,g,!0),i.WASI_ESUCCESS},fd_close:(A)=>{let $=m(A);if(!$)return i.WASI_ERRNO_BADF;if($.node.type==="character"&&$.node.kind==="stdio")return $.node.entry.close(),i.WASI_ESUCCESS;return delete o[A],i.WASI_ESUCCESS},fd_seek:(A,$,f,d)=>{let E=t(),p=m(A);if(!p)return i.WASI_ERRNO_BADF;if(p.node.type==="dir")return i.WASI_ERRNO_ISDIR;if(p.node.type==="character")return i.WASI_ERRNO_IO;let u=S(p.node),g;switch(f){case 0:g=Number($);break;case 1:g=p.position+Number($);break;case 2:g=u+Number($);break;default:return i.WASI_ERRNO_INVAL}if(g<0)return i.WASI_ERRNO_INVAL;return p.position=g,E.setBigUint64(d,BigInt(g),!0),i.WASI_ESUCCESS},fd_tell:(A,$)=>{let f=t(),d=m(A);if(!d)return i.WASI_ERRNO_BADF;if(d.node.type==="dir")return i.WASI_ERRNO_IO;if(d.node.type==="character")return i.WASI_ERRNO_IO;return f.setBigUint64($,BigInt(d.position),!0),i.WASI_ESUCCESS},fd_fdstat_get:(A,$)=>{let f=t(),d=m(A);if(!d)return i.WASI_ERRNO_BADF;let E;switch(d.node.type){case"character":E=i.WASI_FILETYPE_CHARACTER_DEVICE;break;case"dir":E=i.WASI_FILETYPE_DIRECTORY;break;case"file":E=i.WASI_FILETYPE_REGULAR_FILE;break}let p=0x1fffffffn;return r.writeFdstat(f,$,E,0,p,p),i.WASI_ESUCCESS},fd_filestat_get:(A,$)=>{let f=t(),d=m(A);if(!d)return i.WASI_ERRNO_BADF;let E,p=0;switch(d.node.type){case"character":E=i.WASI_FILETYPE_CHARACTER_DEVICE;break;case"dir":E=i.WASI_FILETYPE_DIRECTORY;break;case"file":E=i.WASI_FILETYPE_REGULAR_FILE,p=S(d.node);break}let{atim:u,mtim:g,ctim:y}=s(d.node);return r.writeFilestat(f,$,E,BigInt(p),u,g,y),i.WASI_ESUCCESS},fd_prestat_get:(A,$)=>{let f=t();if(A<3)return i.WASI_ERRNO_BADF;let d=m(A);if(!d||!d.isPreopen)return i.WASI_ERRNO_BADF;f.setUint8($,0);let E=d.preopenPath||"";return f.setUint32($+4,E.length,!0),i.WASI_ESUCCESS},fd_prestat_dir_name:(A,$,f)=>{if(A<3)return i.WASI_ERRNO_BADF;let d=m(A);if(!d||!d.isPreopen)return i.WASI_ERRNO_BADF;let E=d.preopenPath||"",p=t(),u=Math.min(E.length,f);for(let g=0;g<u;g++)p.setUint8($+g,E.charCodeAt(g));return i.WASI_ESUCCESS},fd_open:(A,$,f,d,E,p,u,g)=>{let y=t();if(A<3)return i.WASI_ERRNO_NOTDIR;let k=m(A);if(!k||k.node.type!=="dir")return i.WASI_ERRNO_NOTDIR;let C=r.readString(y,$,f),h=(k.path.endsWith("/")?k.path:`${k.path}/`)+C,b=c(h);if(b)return y.setUint32(g,b.fd,!0),i.WASI_ESUCCESS;let x=n.resolve(k.node,C),T=1,N=2,L=4;if(x){if(d&N)return i.WASI_ERRNO_EXIST;if(d&L){if(x.type!=="file")return i.WASI_ERRNO_INVAL;x.content=new Uint8Array(0)}}else{if(!(d&T))return i.WASI_ERRNO_NOENT;x=n.createFileIn(k.node,C)}return o[l]={node:x,position:0,isPreopen:!1,path:h,fd:l},y.setUint32(g,l,!0),l++,i.WASI_ESUCCESS},path_open:(A,$,f,d,E,p,u,g,y)=>{let k=t();if(A<3)return i.WASI_ERRNO_NOTDIR;let C=m(A);if(!C||C.node.type!=="dir")return i.WASI_ERRNO_NOTDIR;let h=r.readString(k,f,d),b=(C.path.endsWith("/")?C.path:`${C.path}/`)+h,x=c(b);if(x)return k.setUint32(y,x.fd,!0),i.WASI_ESUCCESS;let T=n.resolve(C.node,h),N=1,L=2,le=4;if(T){if(E&L)return i.WASI_ERRNO_EXIST;if(E&le){if(T.type!=="file")return i.WASI_ERRNO_INVAL;T.content=new Uint8Array(0)}}else{if(!(E&N))return i.WASI_ERRNO_NOENT;T=n.createFileIn(C.node,h)}return o[l]={node:T,position:0,isPreopen:!1,path:b,fd:l},k.setUint32(y,l,!0),l++,i.WASI_ESUCCESS},path_filestat_get:(A,$,f,d,E)=>{let p=t(),u=m(A);if(!u)return i.WASI_ERRNO_BADF;if(u.node.type!=="dir")return i.WASI_ERRNO_NOTDIR;let g=r.readString(p,f,d),y=u.path,k=y.endsWith("/")?y+g:`${y}/${g}`,C=n.lookup(k);if(!C)return i.WASI_ERRNO_NOENT;if(C.type==="character"&&C.kind==="stdio")return i.WASI_ERRNO_INVAL;let h,b=0;if(C.type==="dir")h=i.WASI_FILETYPE_DIRECTORY;else if(C.type==="character"&&C.kind==="devnull")h=i.WASI_FILETYPE_CHARACTER_DEVICE;else h=i.WASI_FILETYPE_REGULAR_FILE,b=S(C);let{atim:x,mtim:T,ctim:N}=s(C);return r.writeFilestat(p,E,h,BigInt(b),x,T,N),i.WASI_ESUCCESS}}}}function ae(e,a,r){return{proc_exit:(t)=>{throw new v(t)},proc_raise:(t)=>{return i.WASI_ESUCCESS}}}function re(e,a,r){return{random_get:(t,n)=>{let o=r(),s=new Uint8Array(o.buffer,t,n);return crypto.getRandomValues(s),i.WASI_ESUCCESS}}}class V{wasiImport;instance=null;isStarted=!1;abi;constructor(e){if(this.wasiImport={},this.abi=new i,e?.features){let a={};for(let r of e.features){let t=r.name||"Unknown feature",n=r(e,this.abi,this.view.bind(this));for(let o in n){if(o in this.wasiImport){let s=a[o]||"Unknown feature";throw Error(`Import conflict: Function '${o}' is already provided by '${s}' and is being redefined by '${t}'`)}a[o]=t}this.wasiImport={...this.wasiImport,...n}}}for(let a of i.IMPORT_FUNCTIONS)if(!(a in this.wasiImport))this.wasiImport[a]=()=>{return i.WASI_ENOSYS}}get exports(){if(!this.instance)throw Error("wasi.start() or wasi.initialize() has not been called");return this.instance.exports}view(){if(!this.instance)throw Error("wasi.start() or wasi.initialize() has not been called");if(!this.instance.exports.memory)throw Error("instance.exports.memory is undefined");if(!(this.instance.exports.memory instanceof WebAssembly.Memory))throw Error("instance.exports.memory is not a WebAssembly.Memory");return new DataView(this.instance.exports.memory.buffer)}async initialize(e){if(this.isStarted)throw Error("wasi.start() or wasi.initialize() has already been called");if(this.isStarted=!0,this.instance=e,!this.instance.exports._initialize)throw Error("instance.exports._initialize is undefined");if(typeof this.instance.exports._initialize!=="function")throw Error("instance.exports._initialize is not a function");await this.instance.exports._initialize()}async start(e){if(this.isStarted)throw Error("wasi.start() or wasi.initialize() has already been called");if(this.isStarted=!0,this.instance=e,!this.instance.exports._start)throw Error("instance.exports._start is undefined");if(typeof this.instance.exports._start!=="function")throw Error("instance.exports._start is not a function");try{return await this.instance.exports._start(),i.WASI_ESUCCESS}catch(a){if(a instanceof v)return a.code;throw a}}}var j=new WeakMap,me=new Set(["free","malloc"]);function de(e){return!!e&&(typeof e==="object"||typeof e==="function")&&typeof e.then==="function"}function te(e,a){return new Proxy(e,{get:(r,t)=>a(r[t])})}class H{value=void 0;exports=null;unwrappedExports;constructor(e){this.unwrappedExports=new Set([...me,...e?.unwrappedExports??[]])}getState(){if(!this.exports)throw Error("Exports not initialized");return this.exports.asyncify_get_state()}assertNoneState(){let e=this.getState();if(e!==0)throw Error(`Invalid async state ${e}, expected 0.`)}wrapImportFn(e){return(...a)=>{if(this.getState()===2){if(!this.exports)throw Error("Exports not initialized");return this.exports.asyncify_stop_rewind(),this.value}this.assertNoneState();let r=e(...a);if(!de(r))return r;if(!this.exports)throw Error("Exports not initialized");this.exports.asyncify_start_unwind(16),this.value=r}}wrapModuleImports(e){return te(e,(a)=>{if(typeof a==="function")return this.wrapImportFn(a);return a})}wrapImports(e){if(e===void 0)return;return te(e,(a=Object.create(null))=>this.wrapModuleImports(a))}wrapExportFn(e){let a=j.get(e);if(a!==void 0)return a;return a=async(...r)=>{this.assertNoneState();let t=e(...r);while(this.getState()===1){if(!this.exports)throw Error("Exports not initialized");this.exports.asyncify_stop_unwind(),this.value=await this.value,this.assertNoneState(),this.exports.asyncify_start_rewind(16),t=e(...r)}return this.assertNoneState(),t},j.set(e,a),a}wrapExports(e){let a=Object.create(null);for(let r in e){let t=e[r];if(typeof t==="function"&&!r.startsWith("asyncify_")&&!this.unwrappedExports.has(r))t=this.wrapExportFn(t);Object.defineProperty(a,r,{enumerable:!0,value:t})}return j.set(e,a),a}init(e,a){let r=e.exports,t=r.memory||a?.env&&a.env.memory;if(!t)throw Error("Memory not found in exports or imports.env");let n;if(r.__stack_pointer)n=r.__stack_pointer.value;else n=1024;new Int32Array(t.buffer,16).set([24,n]),this.exports=this.wrapExports(r),Object.setPrototypeOf(e,U.prototype)}}class U extends WebAssembly.Instance{constructor(e,a,r){let t=new H(r);super(e,t.wrapImports(a));t.init(this,a)}get exports(){return j.get(super.exports)}}Object.defineProperty(U.prototype,"exports",{enumerable:!0});async function ne(e,a,r){let t=new H(r),n=await WebAssembly.instantiate(e,t.wrapImports(a));return t.init(n.instance,a),n}var z="./zeroperl-mqcadjqm.wasm";var Ee=["zeroperl_free_interpreter","zeroperl_shutdown","zeroperl_last_error","zeroperl_clear_error","zeroperl_is_initialized","zeroperl_can_evaluate","zeroperl_flush","zeroperl_new_int","zeroperl_new_uint","zeroperl_new_double","zeroperl_new_string","zeroperl_new_bool","zeroperl_new_undef","zeroperl_to_int","zeroperl_to_double","zeroperl_to_string","zeroperl_to_bool","zeroperl_is_undef","zeroperl_get_type","zeroperl_incref","zeroperl_decref","zeroperl_value_free","zeroperl_new_array","zeroperl_array_push","zeroperl_array_pop","zeroperl_array_get","zeroperl_array_set","zeroperl_array_length","zeroperl_array_clear","zeroperl_array_to_value","zeroperl_value_to_array","zeroperl_array_free","zeroperl_new_hash","zeroperl_hash_set","zeroperl_hash_get","zeroperl_hash_exists","zeroperl_hash_delete","zeroperl_hash_clear","zeroperl_hash_iter_new","zeroperl_hash_iter_next","zeroperl_hash_iter_free","zeroperl_hash_to_value","zeroperl_value_to_hash","zeroperl_hash_free","zeroperl_new_ref","zeroperl_deref","zeroperl_is_ref","zeroperl_get_var","zeroperl_get_array_var","zeroperl_get_hash_var","zeroperl_set_var","zeroperl_register_function","zeroperl_register_method","zeroperl_result_get","zeroperl_result_free","zeroperl_set_host_error","zeroperl_get_host_error","zeroperl_clear_host_error"];class F extends Error{exitCode;perlError;constructor(e,a,r){super(e);if(this.name="ZeroPerlError",this.exitCode=a,this.perlError=r,Error.captureStackTrace)Error.captureStackTrace(this,F)}}var Q=new TextDecoder,W=new TextEncoder,K=null;function fe(){return typeof window<"u"&&typeof document<"u"||typeof globalThis.importScripts==="function"}async function Fe(e){if(K){let r=K.deref();if(r)return r}let a;if(fe())a=await(await(e??fetch)(z)).arrayBuffer();else{let t=new URL(z,import.meta.url).pathname;if(typeof Deno<"u")a=(await Deno.readFile(t)).buffer;else if(typeof Bun<"u")a=await Bun.file(t).arrayBuffer();else{let{readFile:n}=await import("node:fs/promises");a=(await n(t)).buffer}}return K=new WeakRef(a),a}function pe(e){return["undef","true","false","int","double","string","array","hash","code","ref"][e]||"undef"}function oe(e){return{void:0,scalar:1,list:2}[e]}class P{ptr;exports;disposed=!1;constructor(e,a){this.ptr=e,this.exports=a}getPtr(){return this.checkDisposed(),this.ptr}toInt(){this.checkDisposed();let e=this.exports.malloc(4);try{if(!this.exports.zeroperl_to_int(this.ptr,e))throw new F("Failed to convert value to int");return new DataView(this.exports.memory.buffer).getInt32(e,!0)}finally{this.exports.free(e)}}toDouble(){this.checkDisposed();let e=this.exports.malloc(8);try{if(!this.exports.zeroperl_to_double(this.ptr,e))throw new F("Failed to convert value to double");return new DataView(this.exports.memory.buffer).getFloat64(e,!0)}finally{this.exports.free(e)}}toString(){this.checkDisposed();let e=this.exports.malloc(4);try{let a=this.exports.zeroperl_to_string(this.ptr,e);if(a===0)return"";let r=new DataView(this.exports.memory.buffer).getUint32(e,!0);return Q.decode(new Uint8Array(this.exports.memory.buffer,a,r))}finally{this.exports.free(e)}}toBoolean(){return this.checkDisposed(),this.exports.zeroperl_to_bool(this.ptr)!==0}isUndef(){return this.checkDisposed(),this.exports.zeroperl_is_undef(this.ptr)!==0}isRef(){return this.checkDisposed(),this.exports.zeroperl_is_ref(this.ptr)!==0}getType(){return this.checkDisposed(),pe(this.exports.zeroperl_get_type(this.ptr))}project(){if(this.checkDisposed(),this.isUndef())return null;switch(this.getType()){case"true":return!0;case"false":return!1;case"int":case"double":return this.toDouble();case"string":return this.toString();default:return this.toString()}}createRef(){this.checkDisposed();let e=this.exports.zeroperl_new_ref(this.ptr);if(e===0)throw new F("Failed to create reference");return new P(e,this.exports)}deref(){this.checkDisposed();let e=this.exports.zeroperl_deref(this.ptr);if(e===0)throw new F("Failed to dereference value");return new P(e,this.exports)}incref(){this.checkDisposed(),this.exports.zeroperl_incref(this.ptr)}decref(){this.checkDisposed(),this.exports.zeroperl_decref(this.ptr)}dispose(){if(this.disposed)return;this.exports.zeroperl_value_free(this.ptr),this.disposed=!0}checkDisposed(){if(this.disposed)throw new F("PerlValue has been disposed")}}class _{ptr;exports;perl;disposed=!1;constructor(e,a,r){this.ptr=e,this.exports=a,this.perl=r}getPtr(){return this.checkDisposed(),this.ptr}push(e){this.checkDisposed();let a=this.perl.toPerlValue(e);try{this.exports.zeroperl_array_push(this.ptr,a.getPtr())}finally{if(!(e instanceof P))a.dispose()}}pop(){this.checkDisposed();let e=this.exports.zeroperl_array_pop(this.ptr);return e===0?null:new P(e,this.exports)}get(e){this.checkDisposed();let a=this.exports.zeroperl_array_get(this.ptr,e);return a===0?null:new P(a,this.exports)}set(e,a){this.checkDisposed();let r=this.perl.toPerlValue(a);try{if(!this.exports.zeroperl_array_set(this.ptr,e,r.getPtr()))throw new F(`Failed to set array element at index ${e}`)}finally{if(!(a instanceof P))r.dispose()}}getLength(){return this.checkDisposed(),this.exports.zeroperl_array_length(this.ptr)}clear(){this.checkDisposed(),this.exports.zeroperl_array_clear(this.ptr)}toValue(){this.checkDisposed();let e=this.exports.zeroperl_array_to_value(this.ptr);if(e===0)throw new F("Failed to convert array to value");return new P(e,this.exports)}project(){this.checkDisposed();let e=this.getLength(),a=[];for(let r=0;r<e;r++){let t=this.get(r);if(t)a.push(t.project()),t.dispose();else a.push(null)}return a}static fromValue(e,a){let r=e.exports,t=r.zeroperl_value_to_array(e.getPtr());return t===0?null:new _(t,r,a)}*[Symbol.iterator](){let e=this.getLength();for(let a=0;a<e;a++){let r=this.get(a);if(r)yield r}}dispose(){if(this.disposed)return;this.exports.zeroperl_array_free(this.ptr),this.disposed=!0}checkDisposed(){if(this.disposed)throw new F("PerlArray has been disposed")}}class D{ptr;exports;perl;disposed=!1;constructor(e,a,r){this.ptr=e,this.exports=a,this.perl=r}getPtr(){return this.checkDisposed(),this.ptr}set(e,a){this.checkDisposed();let r=this.perl.toPerlValue(a),t=this.writeCString(e);try{if(!this.exports.zeroperl_hash_set(this.ptr,t,r.getPtr()))throw new F(`Failed to set hash key '${e}'`)}finally{if(this.exports.free(t),!(a instanceof P))r.dispose()}}get(e){this.checkDisposed();let a=this.writeCString(e);try{let r=this.exports.zeroperl_hash_get(this.ptr,a);return r===0?null:new P(r,this.exports)}finally{this.exports.free(a)}}has(e){this.checkDisposed();let a=this.writeCString(e);try{return this.exports.zeroperl_hash_exists(this.ptr,a)!==0}finally{this.exports.free(a)}}delete(e){this.checkDisposed();let a=this.writeCString(e);try{return this.exports.zeroperl_hash_delete(this.ptr,a)!==0}finally{this.exports.free(a)}}clear(){this.checkDisposed(),this.exports.zeroperl_hash_clear(this.ptr)}toValue(){this.checkDisposed();let e=this.exports.zeroperl_hash_to_value(this.ptr);if(e===0)throw new F("Failed to convert hash to value");return new P(e,this.exports)}project(){this.checkDisposed();let e={};for(let[a,r]of this.entries())e[a]=r.project(),r.dispose();return e}static fromValue(e,a){let r=e.exports,t=r.zeroperl_value_to_hash(e.getPtr());return t===0?null:new D(t,r,a)}*entries(){this.checkDisposed();let e=this.exports.zeroperl_hash_iter_new(this.ptr);if(e===0)throw new F("Failed to create hash iterator");let a=this.exports.malloc(4),r=this.exports.malloc(4);try{while(this.exports.zeroperl_hash_iter_next(e,a,r)){let t=new DataView(this.exports.memory.buffer),n=t.getUint32(a,!0),o=t.getUint32(r,!0);yield[this.readCString(n),new P(o,this.exports)]}}finally{this.exports.free(a),this.exports.free(r),this.exports.zeroperl_hash_iter_free(e)}}*keys(){for(let[e,a]of this.entries())a.dispose(),yield e}*values(){for(let[,e]of this.entries())yield e}dispose(){if(this.disposed)return;this.exports.zeroperl_hash_free(this.ptr),this.disposed=!0}writeCString(e){let a=W.encode(`${e}\x00`),r=this.exports.malloc(a.length);return new Uint8Array(this.exports.memory.buffer).set(a,r),r}readCString(e){if(e===0)return"";let a=new Uint8Array(this.exports.memory.buffer),r=0;while(a[e+r]!==0)r++;return Q.decode(a.subarray(e,e+r))}checkDisposed(){if(this.disposed)throw new F("PerlHash has been disposed")}}class R{wasi;isDisposed=!1;hostFunctions=new Map;nextFuncId=1;constructor(e){this.wasi=e}get exports(){return this.wasi.exports}static async create(e={}){let a=await Fe(e.fetch),r=e.fileSystem||new I({"/":""}),t={env:e.env||{},args:["zeroperl"],features:[Z,Y,re,J,ae,q({withFileSystem:r,withStdIo:{stdout:(m)=>e.stdout?.(m),stderr:(m)=>e.stderr?.(m),outputBuffers:e.outputBuffers}})]},n=new V(t),o=new R(n),s=async(m,S,A)=>o.handleHostCall(m,S,A),{instance:l}=await ne(a,{wasi_snapshot_preview1:n.wasiImport,env:{call_host_function:s}},{unwrappedExports:Ee});await n.initialize(l);let c=await o.exports.zeroperl_init();if(c!==0)throw new F("Failed to initialize Perl interpreter",c,o.getLastError());return o}async handleHostCall(e,a,r){let t=this.hostFunctions.get(e);if(!t)return this.setHostError(`Host function ${e} not found`),0;try{let n=[];if(a>0){let l=new DataView(this.exports.memory.buffer);for(let c=0;c<a;c++){let m=l.getUint32(r+c*4,!0);if(m!==0)n.push(new P(m,this.exports))}}let o=await t(...n);if(o instanceof P)return o.getPtr();let s=this.exports.zeroperl_new_undef();if(s===0)return this.setHostError("Failed to allocate return value"),0;return s}catch(n){return this.setHostError(n instanceof Error?n.message:String(n)),0}}setHostError(e){let a=this.writeCString(e);if(a)this.exports.zeroperl_set_host_error(a),this.exports.free(a)}createInt(e){this.checkDisposed();let a=this.exports.zeroperl_new_int(Math.floor(e));if(a===0)throw new F("Failed to create integer value");return new P(a,this.exports)}createUInt(e){this.checkDisposed();let a=this.exports.zeroperl_new_uint(Math.floor(Math.abs(e)));if(a===0)throw new F("Failed to create unsigned integer value");return new P(a,this.exports)}createDouble(e){this.checkDisposed();let a=this.exports.zeroperl_new_double(e);if(a===0)throw new F("Failed to create double value");return new P(a,this.exports)}createString(e){this.checkDisposed();let a=W.encode(e),r=this.exports.malloc(a.length);new Uint8Array(this.exports.memory.buffer).set(a,r);try{let t=this.exports.zeroperl_new_string(r,a.length);if(t===0)throw new F("Failed to create string value");return new P(t,this.exports)}finally{this.exports.free(r)}}createBool(e){this.checkDisposed();let a=this.exports.zeroperl_new_bool(e?1:0);if(a===0)throw new F("Failed to create boolean value");return new P(a,this.exports)}createUndef(){this.checkDisposed();let e=this.exports.zeroperl_new_undef();if(e===0)throw new F("Failed to create undef value");return new P(e,this.exports)}createArray(e){this.checkDisposed();let a=this.exports.zeroperl_new_array();if(a===0)throw new F("Failed to create array");let r=new _(a,this.exports,this);if(e)for(let t of e)r.push(t);return r}createHash(e){this.checkDisposed();let a=this.exports.zeroperl_new_hash();if(a===0)throw new F("Failed to create hash");let r=new D(a,this.exports,this);if(e)for(let[t,n]of Object.entries(e))r.set(t,n);return r}toPerlValue(e){if(e instanceof P)return e;if(e===null||e===void 0)return this.createUndef();if(typeof e==="boolean")return this.createBool(e);if(typeof e==="number")return Number.isInteger(e)?this.createInt(e):this.createDouble(e);if(typeof e==="string")return this.createString(e);if(Array.isArray(e)){let a=this.createArray(e),r=a.toValue();return a.dispose(),r}if(typeof e==="object"){let a=this.createHash(e),r=a.toValue();return a.dispose(),r}throw new F(`Cannot convert value of type ${typeof e} to PerlValue`)}getVariable(e){this.checkDisposed();let a=this.writeCString(e);try{let r=this.exports.zeroperl_get_var(a);return r===0?null:new P(r,this.exports)}finally{this.exports.free(a)}}getArrayVariable(e){this.checkDisposed();let a=this.writeCString(e);try{let r=this.exports.zeroperl_get_array_var(a);return r===0?null:new _(r,this.exports,this)}finally{this.exports.free(a)}}getHashVariable(e){this.checkDisposed();let a=this.writeCString(e);try{let r=this.exports.zeroperl_get_hash_var(a);return r===0?null:new D(r,this.exports,this)}finally{this.exports.free(a)}}setVariable(e,a){this.checkDisposed();let r=this.toPerlValue(a),t=this.writeCString(e);try{if(!this.exports.zeroperl_set_var(t,r.getPtr()))throw new F(`Failed to set variable '${e}'`)}finally{if(this.exports.free(t),!(a instanceof P))r.dispose()}}registerFunction(e,a){this.checkDisposed();let r=this.nextFuncId++;this.hostFunctions.set(r,a);let t=this.writeCString(e);try{this.exports.zeroperl_register_function(r,t)}finally{this.exports.free(t)}}registerMethod(e,a,r){this.checkDisposed();let t=this.nextFuncId++;this.hostFunctions.set(t,r);let n=this.writeCString(e),o=this.writeCString(a);try{this.exports.zeroperl_register_method(t,n,o)}finally{this.exports.free(n),this.exports.free(o)}}async call(e,a=[],r="scalar"){this.checkDisposed();let t=this.writeCString(e),n=oe(r),o=0;if(a.length>0){o=this.exports.malloc(a.length*4);let s=new DataView(this.exports.memory.buffer);for(let l=0;l<a.length;l++){let c=a[l];if(!c)throw new F(`Argument at index ${l} is undefined`);s.setUint32(o+l*4,c.getPtr(),!0)}}try{let s=await this.exports.zeroperl_call(t,n,a.length,o);if(s===0){if(r==="void")return;if(r==="scalar")return null;return[]}let l=new DataView(this.exports.memory.buffer),c=l.getInt32(s,!0),m=[];for(let A=0;A<c;A++){let $=this.exports.zeroperl_result_get(s,A);if($!==0)m.push(new P($,this.exports))}let S=l.getUint32(s+4,!0);if(S!==0)this.exports.free(S);if(this.exports.free(s),r==="void"){for(let A of m)A.dispose();return}if(r==="scalar")return m[0]??null;return m}catch(s){if(s instanceof v){if(r==="void")return;if(r==="scalar")return null;return[]}throw s}finally{if(this.exports.free(t),o!==0)this.exports.free(o)}}async eval(e,a=[]){this.checkDisposed();let r=this.writeCString(e),t=0,n=[];if(a.length>0){let o=this.writeStringArray(a);t=o.argv,n=o.buffers}try{let o=await this.exports.zeroperl_eval(r,oe("scalar"),a.length,t);if(o!==0)return{success:!1,error:this.getLastError(),exitCode:o};return{success:!0,exitCode:0}}catch(o){if(o instanceof v){if(o.code!==0)return{success:!1,error:this.getLastError(),exitCode:o.code};return{success:!0,exitCode:0}}throw o}finally{if(this.exports.free(r),n.length>0)this.freeStringArray(t,n)}}async runFile(e,a=[]){this.checkDisposed();let r=this.writeCString(e),t=0,n=[];if(a.length>0){let o=this.writeStringArray(a);t=o.argv,n=o.buffers}try{let o=await this.exports.zeroperl_run_file(r,a.length,t);if(o!==0)return{success:!1,error:this.getLastError(),exitCode:o};return{success:!0,exitCode:0}}catch(o){if(o instanceof v){if(o.code!==0)return{success:!1,error:this.getLastError(),exitCode:o.code};return{success:!0,exitCode:0}}throw o}finally{if(this.exports.free(r),n.length>0)this.freeStringArray(t,n)}}async reset(){this.checkDisposed();let e=await this.exports.zeroperl_reset();if(e!==0)throw new F("Failed to reset Perl interpreter",e,this.getLastError())}flush(){if(this.checkDisposed(),this.exports.zeroperl_flush()!==0)throw new F("Failed to flush output buffers")}getLastError(){return this.checkDisposed(),this.readCString(this.exports.zeroperl_last_error())}clearError(){this.checkDisposed(),this.exports.zeroperl_clear_error()}isInitialized(){return this.checkDisposed(),this.exports.zeroperl_is_initialized()!==0}canEvaluate(){return this.checkDisposed(),this.exports.zeroperl_can_evaluate()!==0}dispose(){if(this.isDisposed)return;this.exports.zeroperl_free_interpreter(),this.isDisposed=!0,this.hostFunctions.clear()}shutdown(){if(this.isDisposed)return;this.exports.zeroperl_shutdown(),this.isDisposed=!0,this.hostFunctions.clear()}writeCString(e){if(!e)return 0;let a=W.encode(`${e}\x00`),r=this.exports.malloc(a.length);return new Uint8Array(this.exports.memory.buffer).set(a,r),r}readCString(e){if(e===0)return"";let a=new Uint8Array(this.exports.memory.buffer),r=0;while(a[e+r]!==0)r++;return Q.decode(a.subarray(e,e+r))}writeStringArray(e){let a=[],r=this.exports.malloc(e.length*4),t=new DataView(this.exports.memory.buffer);for(let n=0;n<e.length;n++){let o=e[n];if(o===void 0)throw new F(`Argument at index ${n} is undefined`);let s=this.writeCString(o);a.push(s),t.setUint32(r+n*4,s,!0)}return{argv:r,buffers:a}}freeStringArray(e,a){for(let r of a)this.exports.free(r);this.exports.free(e)}checkDisposed(){if(this.isDisposed)throw new F("ZeroPerl instance has been disposed")}}var ie=`#!/usr/bin/env perl
use strict;
use warnings;
require 5.004;

my $version = '13.59';

$^W = 1;

my $exePath;

BEGIN {
    $exePath = @ARGV && lc( $ARGV[0] ) eq '-xpath' && shift() ? $^X : $0;
    my $exeDir = ( $exePath =~ /(.*)[\\\\\\/]/ ) ? $1       : '.';
    my $incDir = ( $0       =~ /(.*)[\\\\\\/]/ ) ? "$1/lib" : './lib';
    if ( -l $0 ) {
        my $lnk = eval { readlink $0 };
        if ( defined $lnk ) {
            my $lnkDir = ( $lnk =~ /(.*)[\\\\\\/]/ ) ? $1 : '.';
            $exeDir = ( ( $lnk =~ m(^/) ) ? '' : $exeDir . '/' ) . $lnkDir;
            $incDir = "$exeDir/lib";
        }
    }
    $Image::ExifTool::exeDir = $exeDir;

    unshift @INC, $incDir;
    while ( @ARGV and lc( $ARGV[0] ) eq '-config' ) {
        shift;
        push @Image::ExifTool::configFiles, shift;
    }
}
use Image::ExifTool qw{:Public};

sub SigInt();
sub SigCont();
sub Cleanup();
sub GetImageInfo($$);
sub SetImageInfo($$$);
sub DoHardLink($$$$$);
sub CleanXML($);
sub EncodeXML($);
sub FormatXML($$$);
sub EscapeJSON($;$);
sub FormatJSON($$$;$);
sub PrintCSV(;$);
sub AddGroups($$$$);
sub ConvertBinary($);
sub IsEqual($$;$);
sub Printable($);
sub LengthUTF8($);
sub Infile($;$);
sub AddSetTagsFile($;$);
sub Warning($$);
sub DoSetFromFile($$$);
sub CleanFilename($);
sub HasWildcards($);
sub SetWindowTitle($);
sub ProcessFiles($;$);
sub ScanDir($$;$);
sub FindFileWindows($$);
sub FileNotFound($);
sub PreserveTime();
sub AbsPath($);
sub MyConvertFileName($$);
sub SuggestedExtension($$$);
sub LoadPrintFormat($;$);
sub FilenameSPrintf($;$@);
sub NextUnusedFilename($;$);
sub CreateDirectory($);
sub OpenOutputFile($;@);
sub AcceptFile($);
sub SlurpFile($$);
sub FilterArgfileLine($);
sub ReadStayOpen($);
sub Progress($$);
sub PrintTagList($@);
sub PrintErrors($$$);
sub GetASCII($);

END {
    Cleanup();
}

my @commonArgs;
my @condition;
my @csvExclude;
my @csvFiles;
my @csvTags;
my @delFiles;
my @dynamicFiles;
my ( @echo3, @echo4 );
my @efile;
my @exclude;
my @files;
my @moreArgs;
my @newValues;
my @requestTags;
my @srcFmt;
my @tags;
my %altFile;
my %appended;
my %countLink;
my %created;
my %csvTags;
my %database;
my %filterExt;
my %ignore;
my %outComma;
my %outTrailer;
my %preserveTime;
my %printFmt;
my %seqFileDir;
my %setTags;
my %setTagsList;
my %usedFileName;
my %utf8FileName;
my %warnedOnce;
my %wext;
my %wroteHEAD;
my $allGroup;
my $altEnc;
my $argFormat;
my $binaryOutput;
my $binaryStdout;
my $binSep;
my $binTerm;
my $comma;
my $count;
my $countBad;
my $countBadCr;
my $countBadWr;
my $countCopyWr;
my $countDir;
my $countFailed;
my $countGoodCr;
my $countGoodWr;
my $countNewDir;
my $countSameWr;
my $critical;
my $csv;
my $csvDelim;
my $dbAdd;
my $dbSaveCount;
my $deleteOrig;
my $diff;
my $disableOutput;
my $doSetFileName;
my $doUnzip;
my ( $end, $endDir, %endDir );
my $escapeC;
my $escapeHTML;
my $evalWarning;
my $executeID;
my $failCondition;
my $fastCondition;
my $fileHeader;
my $fileTrailer;
my $filtered;
my $filterFlag;
my $fixLen;
my $forcePrint;
my $geoOnly;
my $helped;
my $html;
my $ignoreHidden;
my $interrupted;
my $isBinary;
my $isWriting;
my $joinLists;
my $json;
my $langOpt;
my $listDir;
my $listItem;
my $listSep;
my $mt;
my $multiFile;
my $noBinary;
my $outFormat;
my $outOpt;
my $overwriteOrig;
my $pause;
my $plot;
my $preserveTime;
my $progress;
my $progressCount;
my $progressIncr;
my $progressMax;
my $progressNext;
my $progStr;
my $purge;
my $quiet;
my $rafStdin;
my $recurse;
my $rtnVal;
my $rtnValPrev;
my $saveCount;
my $scanWritable;
my $sectHeader;
my $sectTrailer;
my $seqFileDir;
my $seqFileNum;
my $setCharset;
my $showGroup;
my $showTagID;
my $stayOpenBuff = '';
my $stayOpenFile;
my $structOpt;
my $tabFormat;
my $tagOut;
my $textOut;
my $textOut2;
my $textOverwrite;
my $tmpFile;
my $tmpText;
my $validFile;
my $verbose;
my $vout;
my $windowTitle;
my $xml;

my $stayOpen = 0;

my $rtnValApp = 0;
my $curTitle  = '';

my $isCRLF = { MSWin32 => 1, os2 => 1, dos => 1 }->{$^O};

my %jsonChar =
  ( '"' => '"', '\\\\' => '\\\\', "\\t" => 't', "\\n" => 'n', "\\r" => 'r' );

my %escC   = ( "\\n" => '\\n', "\\r" => '\\r', "\\t" => '\\t', '\\\\' => '\\\\\\\\' );
my %unescC = (
    a    => "\\a",
    b    => "\\b",
    f    => "\\f",
    n    => "\\n",
    r    => "\\r",
    t    => "\\t",
    0    => "\\0",
    '\\\\' => '\\\\'
);

my %optArgs = (
    '-tagsfromfile'    => 1,
    '-addtagsfromfile' => 1,
    '-alltagsfromfile' => 1,
    '-@'               => 1,
    '-api'             => 1,
    '-c'               => 1,
    '-coordformat'     => 1,
    '-charset'         => 0,
    '-config'          => 1,
    '-csvdelim'        => 1,
    '-d'               => 1,
    '-dateformat'      => 1,
    '-D'               => 0,
    '-diff'            => 1,
    '-echo'            => 1,
    '-echo#'           => 1,
    '-efile'           => 1,
    '-efile#'          => 1,
    '-efile!'          => 1,
    '-efile#!'         => 1,
    '-ext'             => 1,
    '--ext'            => 1,
    '-ext+'            => 1,
    '--ext+'           => 1,
    '-extension'       => 1,
    '--extension'      => 1,
    '-extension+'      => 1,
    '--extension+'     => 1,
    '-fileorder'       => 1,
    '-fileorder#'      => 1,
    '-file#'           => 1,
    '-geotag'          => 1,
    '-globaltimeshift' => 1,
    '-i'               => 1,
    '-ignore'          => 1,
    '-if'              => 1,
    '-if#'             => 1,
    '-lang'            => 0,
    '-listitem'        => 1,
    '-o'               => 1,
    '-out'             => 1,
    '-p'               => 1,
    '-printformat'     => 1,
    '-p-'              => 1,
    '-printformat-'    => 1,
    '-P'               => 0,
    '-password'        => 1,
    '-require'         => 1,
    '-sep'             => 1,
    '-separator'       => 1,
    '-srcfile'         => 1,
    '-stay_open'       => 1,
    '-use'             => 1,
    '-userparam'       => 1,
    '-w'               => 1,
    '-w!'              => 1,
    '-w+'              => 1,
    '-w+!'             => 1,
    '-w!+'             => 1,
    '-textout'         => 1,
    '-textout!'        => 1,
    '-textout+'        => 1,
    '-textout+!'       => 1,
    '-textout!+'       => 1,
    '-tagout'          => 1,
    '-tagout!'         => 1,
    '-tagout+'         => 1,
    '-tagout+!'        => 1,
    '-tagout!+'        => 1,
    '-wext'            => 1,
    '-wm'              => 1,
    '-writemode'       => 1,
    '-x'               => 1,
    '-exclude'         => 1,
    '-X'               => 0,
);

my @recommends = qw(
  Archive::Zip
  Compress::Zlib
  Digest::MD5
  Digest::SHA
  IO::Compress::Bzip2
  POSIX::strptime
  Time::Local
  Unicode::LineBreak
  File::StatX
  Compress::Raw::Lzma
  IO::Compress::RawDeflate
  IO::Uncompress::RawInflate
  IO::Compress::Brotli
  IO::Uncompress::Brotli
  Win32::API
  Win32::FindFile
  Win32API::File
);
my %altRecommends = ( 'POSIX::strptime' => 'Time::Piece', );

my %unescapeChar = ( 't' => "\\t", 'n' => "\\n", 'r' => "\\r" );

sub Image::ExifTool::EndDir() { return $endDir = 1 }
sub Image::ExifTool::End()    { return $end    = 1 }

sub Exit {
    if ($pause) {
        if ( eval { require Term::ReadKey } ) {
            print STDERR "-- press any key --";
            Term::ReadKey::ReadMode('cbreak');
            Term::ReadKey::ReadKey(0);
            Term::ReadKey::ReadMode(0);
            print STDERR "\\b \\b" x 20;
        }
        else {
            print STDERR "-- press RETURN --\\n";
            <STDIN>;
        }
    }
    exit shift;
}

sub Warn {
    if ( $quiet < 2 or $_[0] =~ /^Error/ ) {
        my $oldWarn = $SIG{'__WARN__'};
        delete $SIG{'__WARN__'};
        warn(@_);
        $SIG{'__WARN__'} = $oldWarn if defined $oldWarn;
    }
}
sub Error { Warn @_; $rtnVal = 1; }

sub WarnOnce($) {
    Warn(@_) and $warnedOnce{ $_[0] } = 1 unless $warnedOnce{ $_[0] };
}

sub SigInt() {
    $critical and $interrupted = 1, return;
    Cleanup();
    exit 1;
}
sub SigCont() { }

sub Cleanup() {
    $mt->Unlink($tmpFile) if defined $tmpFile;
    $mt->Unlink($tmpText) if defined $tmpText;
    undef $tmpFile;
    undef $tmpText;
    PreserveTime() if %preserveTime;
    SetWindowTitle('');
}

if ( grep /^-common_args$/i, @ARGV ) {
    my ( @newArgs, $common, $end );
    foreach (@ARGV) {
        if ( /^-common_args$/i and not $end ) {
            $common = 1;
        }
        elsif ($common) {
            push @commonArgs, $_;
        }
        else {
            $end = 1 if $_ eq '--';
            push @newArgs, $_;
        }
    }
    @ARGV = @newArgs if $common;
}

Command: for ( ; ; ) {

    if (@echo3) {
        my $str = join( "\\n", @echo3 ) . "\\n";
        $str =~ s/\\$\\{status\\}/$rtnVal/ig;
        print STDOUT $str;
    }
    if (@echo4) {
        my $str = join( "\\n", @echo4 ) . "\\n";
        $str =~ s/\\$\\{status\\}/$rtnVal/ig;
        print STDERR $str;
    }

    $rafStdin->Close() if $rafStdin;
    undef $rafStdin;

    $rtnValPrev = $rtnVal;
    $rtnValApp  = $rtnVal if $rtnVal;

    last unless @ARGV or not defined $rtnVal or $stayOpen >= 2 or @commonArgs;

    if ($binaryStdout) {
        binmode( STDOUT, ':crlf' ) if $] >= 5.006 and $isCRLF;
        $binaryStdout = 0;
    }

    if ( $stayOpen >= 2 ) {
        if ( $quiet and not defined $executeID ) {
            eval { require IO::Handle } and STDERR->flush(), STDOUT->flush();
        }
        else {
            eval { require IO::Handle } and STDERR->flush();
            my $id   = defined $executeID ? $executeID : '';
            my $save = $|;
            $| = 1;
            print "{ready$id}\\n";
            $| = $save;
        }
    }

    undef @condition;
    undef @csvExclude;
    undef @csvFiles;
    undef @csvTags;
    undef @delFiles;
    undef @dynamicFiles;
    undef @echo3;
    undef @echo4;
    undef @efile;
    undef @exclude;
    undef @files;
    undef @newValues;
    undef @requestTags;
    undef @srcFmt;
    undef @tags;
    undef %altFile;
    undef %appended;
    undef %countLink;
    undef %created;
    undef %csvTags;
    undef %database;
    undef %endDir;
    undef %filterExt;
    undef %ignore;
    undef %outComma;
    undef %outTrailer;
    undef %preserveTime;
    undef %printFmt;
    undef %seqFileDir;
    undef %setTags;
    undef %setTagsList;
    undef %usedFileName;
    undef %utf8FileName;
    undef %warnedOnce;
    undef %wext;
    undef %wroteHEAD;
    undef $allGroup;
    undef $altEnc;
    undef $argFormat;
    undef $binaryOutput;
    undef $binSep;
    undef $binTerm;
    undef $comma;
    undef $csv;
    undef $dbAdd;
    undef $deleteOrig;
    undef $diff;
    undef $disableOutput;
    undef $doSetFileName;
    undef $doUnzip;
    undef $end;
    undef $endDir;
    undef $escapeC;
    undef $escapeHTML;
    undef $evalWarning;
    undef $executeID;
    undef $failCondition;
    undef $fastCondition;
    undef $fileHeader;
    undef $filtered;
    undef $fixLen;
    undef $forcePrint;
    undef $geoOnly;
    undef $ignoreHidden;
    undef $isBinary;
    undef $joinLists;
    undef $langOpt;
    undef $listDir;
    undef $listItem;
    undef $multiFile;
    undef $noBinary;
    undef $outOpt;
    undef $plot;
    undef $preserveTime;
    undef $progress;
    undef $progressCount;
    undef $progressIncr;
    undef $progressMax;
    undef $progressNext;
    undef $purge;
    undef $rafStdin;
    undef $recurse;
    undef $scanWritable;
    undef $sectHeader;
    undef $setCharset;
    undef $showGroup;
    undef $showTagID;
    undef $structOpt;
    undef $tagOut;
    undef $textOut;
    undef $textOut2;
    undef $textOverwrite;
    undef $tmpFile;
    undef $tmpText;
    undef $validFile;
    undef $verbose;
    undef $windowTitle;

    $count         = 0;
    $countBad      = 0;
    $countBadCr    = 0;
    $countBadWr    = 0;
    $countCopyWr   = 0;
    $countDir      = 0;
    $countFailed   = 0;
    $countGoodCr   = 0;
    $countGoodWr   = 0;
    $countNewDir   = 0;
    $countSameWr   = 0;
    $csvDelim      = ',';
    $dbSaveCount   = 0;
    $fileTrailer   = '';
    $filterFlag    = 0;
    $html          = 0;
    $isWriting     = 0;
    $json          = 0;
    $listSep       = ', ';
    $outFormat     = 0;
    $overwriteOrig = 0;
    $progStr       = '';
    $quiet         = 0;
    $rtnVal        = 0;
    $saveCount     = 0;
    $sectTrailer   = '';
    $seqFileDir    = 0;
    $seqFileNum    = 0;
    $tabFormat     = 0;
    $vout          = \\*STDOUT;
    $xml           = 0;

    my @fileOrder;
    my $fileOrderFast;
    my $addGeotime;
    my $doGlob;
    my $endOfOpts;
    my $escapeXML;
    my $setTagsFile;
    my $sortOpt;
    my $srcStdin;
    my $tagsFrom = '';
    my $useMWG;

    my ( $argsLeft, @nextPass, $badCmd );
    my $pass = 0;

    if ( $^O eq 'MSWin32' and eval { require File::Glob } ) {
        import File::Glob qw(:globally :nocase);
        $doGlob = 1;
    }

    $mt = Image::ExifTool->new;

    $mt->Options( Duplicates => 0 )
      unless %Image::ExifTool::UserDefined::Options
      and defined $Image::ExifTool::UserDefined::Options{Duplicates};

    $joinLists = 1 if defined $mt->Options('List') and not $mt->Options('List');

    if ( not $preserveTime and $^O eq 'MSWin32' ) {
        $preserveTime = 2
          if eval { require Win32::API } and eval { require Win32API::File };
    }

    if (@Image::ExifTool::UserDefined::Arguments) {
        unshift @ARGV, @Image::ExifTool::UserDefined::Arguments;
    }

    if ( $version ne $Image::ExifTool::VERSION ) {
        Warn
"Application version $version does not match Image::ExifTool library version $Image::ExifTool::VERSION\\n";
    }

    for ( ; ; ) {

        if (
            not @ARGV
            or ( $ARGV[0] =~ /^(-|\\xe2\\x88\\x92)execute(\\d+)?$/i
                and not $endOfOpts )
          )
        {
            if (@ARGV) {
                $executeID = $2;
                $helped    = 1;
                $badCmd and shift, $rtnVal = 1, next Command;
            }
            elsif ( $stayOpen >= 2 ) {
                ReadStayOpen( \\@ARGV );
                next;
            }
            elsif ($badCmd) {
                undef @commonArgs;
                $rtnVal = 1;
                next Command;
            }
            if ( $pass == 0 ) {
                if ( @commonArgs and not defined $argsLeft ) {
                    $argsLeft = scalar(@ARGV) + scalar(@moreArgs);
                    unshift @ARGV, @commonArgs;
                    undef @commonArgs unless $argsLeft;
                    next;
                }
                if ( defined $argsLeft
                    and $argsLeft < scalar(@ARGV) + scalar(@moreArgs) )
                {
                    Warn
"Ignoring -common_args from $ARGV[0] onwards to avoid infinite recursion\\n";
                    while ( $argsLeft < scalar(@ARGV) + scalar(@moreArgs) ) {
                        @ARGV and shift(@ARGV), next;
                        shift @moreArgs;
                    }
                }
                $useMWG = 1
                  if not $useMWG
                  and grep /^([--_0-9A-Z]+:)*1?mwg:/i, @tags, @requestTags;
                if ($useMWG) {
                    require Image::ExifTool::MWG;
                    Image::ExifTool::MWG::Load();
                }
                if ( defined $forcePrint ) {
                    unless ( defined $mt->Options('MissingTagValue') ) {
                        $mt->Options( MissingTagValue => '-' );
                    }
                    $forcePrint = $mt->Options('MissingTagValue');
                }
            }
            if (@nextPass) {
                unshift @ARGV, @nextPass;
                undef @nextPass;
                undef $endOfOpts;
                ++$pass;
                next;
            }
            @ARGV and shift;
            last;
        }
        $_ = shift;
        next if $badCmd;

        if ( not $endOfOpts and s/^(-|\\xe2\\x88\\x92)// ) {
            s/^\\xe2\\x88\\x92/-/;
            if ( $_ eq '-' ) {
                $pass or push @nextPass, '--';
                $endOfOpts = 1;
                next;
            }
            my $a = lc $_;
            if (/^list([wfrdx]|wf|g(\\d*)|geo)?$/i) {
                $pass or push @nextPass, "-$_";
                my $type = lc( $1 || '' );
                if ( not $type or $type eq 'w' or $type eq 'x' ) {
                    my $group;
                    if (    $ARGV[0]
                        and $ARGV[0] =~ /^(-|\\xe2\\x88\\x92)(.+):(all|\\*)$/i )
                    {
                        if ( $pass == 0 ) {
                            $useMWG = 1 if lc($2) eq 'mwg';
                            push @nextPass, shift;
                            next;
                        }
                        $group = $2;
                        shift;
                        $group =~ /IFD/i
                          and Warn("Can't list tags for specific IFD\\n"),
                          $helped = 1, next;
                        $group =~ /^(all|\\*)$/ and undef $group;
                    }
                    else {
                        $pass or next;
                    }
                    $helped = 1;
                    if ( $type eq 'x' ) {
                        require Image::ExifTool::TagInfoXML;
                        my %opts;
                        $opts{Flags}  = 1 if defined $forcePrint;
                        $opts{NoDesc} = 1 if $outFormat > 0;
                        $opts{Lang}   = $langOpt;
                        Image::ExifTool::TagInfoXML::Write( undef, $group,
                            %opts );
                        next;
                    }
                    my $wr  = ( $type eq 'w' );
                    my $msg = ( $wr ? 'Writable' : 'Available' )
                      . ( $group ? " $group" : '' ) . ' tags';
                    PrintTagList( $msg,
                        $wr ? GetWritableTags($group) : GetAllTags($group) );
                    next if $group or $wr;
                    my @tagList = GetShortcuts();
                    PrintTagList( 'Command-line shortcuts', @tagList )
                      if @tagList;
                    next;
                }
                $pass or next;
                $helped = 1;
                if ( $type eq 'wf' ) {
                    my @wf;
                    CanWrite($_) and push @wf, $_ foreach GetFileType();
                    PrintTagList( 'Writable file extensions', @wf );
                }
                elsif ( $type eq 'f' ) {
                    PrintTagList( 'Supported file extensions', GetFileType() );
                }
                elsif ( $type eq 'r' ) {
                    PrintTagList(
                        'Recognized file extensions',
                        GetFileType( undef, 0 )
                    );
                }
                elsif ( $type eq 'd' ) {
                    PrintTagList( 'Deletable groups', GetDeleteGroups() );
                }
                elsif ( $type eq 'geo' ) {
                    require Image::ExifTool::Geolocation;
                    my ( $i, $entry );
                    print "Geolocation database:\\n" unless $quiet;
                    my $isAlt =
                      $mt->Options('GeolocAltNames') ? ',AltNames' : '';
                    $isAlt = ''
                      if $isAlt
                      and not Image::ExifTool::Geolocation::ReadAltNames();
                    print
"City,Region,Subregion,CountryCode,Country,TimeZone,FeatureCode,Population,Latitude,Longitude$isAlt\\n";
                    Image::ExifTool::Geolocation::SortDatabase('City')
                      if $sortOpt;
                    my $minPop  = $mt->Options('GeolocMinPop');
                    my $feature = $mt->Options('GeolocFeature') || '';
                    my $neg     = $feature =~ s/^-//;
                    my %fcodes  = map { lc($_) => 1 } split /\\s*,\\s*/, $feature;
                    my @isUTF8  = ( 0, 1, 2, 4 );
                    push @isUTF8, 10 if $isAlt;

                    for ( $i = 0 ; ; ++$i ) {
                        my @entry =
                          Image::ExifTool::Geolocation::GetEntry( $i, $langOpt,
                            1 )
                          or last;
                        $#entry = 9;
                        next if $minPop and $entry[7] < $minPop;
                        next
                          if %fcodes
                          and $neg
                          ? $fcodes{ lc $entry[6] }
                          : not $fcodes{ lc $entry[6] };
                        push @entry,
                          Image::ExifTool::Geolocation::GetAltNames( $i, 1 )
                          if $isAlt;
                        $_ = defined $_ ? $mt->Decode( $_, 'UTF8' ) : ''
                          foreach @entry[@isUTF8];
                        pop @entry if $isAlt and not $entry[10];
                        print join( ',', @entry ), "\\n";
                    }
                }
                else {

                    my $family = $2 || 0;
                    PrintTagList(
                        "Groups in family $family",
                        $mt->GetAllGroups($family)
                    );
                }
                next;
            }
            if ( $a eq 'ver' ) {
                $pass or push( @nextPass, '-ver' ), next;
                my $libVer = $Image::ExifTool::VERSION;
                my $str =
                  $libVer eq $version
                  ? ''
                  : " [Warning: Library version is $libVer]";
                if ($verbose) {
                    print
"ExifTool version $version$str$Image::ExifTool::RELEASE\\n";
                    printf "Perl version %s%s\\n", $],
                      ( defined \${^UNICODE} ? " (-C\${^UNICODE})" : '' );
                    print "Platform: $^O\\n";
                    if ( $verbose > 8 ) {
                        print "Current Dir: " . Cwd::getcwd() . "\\n"
                          if ( eval { require Cwd } );
                        print "Script Name: $0\\n";
                        print "Exe Name:    $^X\\n";
                        print "Exe Dir:     $Image::ExifTool::exeDir\\n";
                        print "Exe Path:    $exePath\\n";
                    }
                    print "Optional libraries:\\n";
                    foreach (@recommends) {
                        next if /^Win32/ and $^O ne 'MSWin32';
                        next if /StatX/  and $^O ne 'linux';
                        my $ver = eval "require $_ and \\$\${_}::VERSION";
                        my $alt = $altRecommends{$_};
                        $ver = eval "require $alt and \\$\${alt}::VERSION"
                          and $_ = $alt
                          if not $ver and $alt;
                        printf "  %-28s %s\\n", $_, $ver || '(not installed)';
                    }
                    if ( $verbose > 1 ) {
                        print "Include directories:\\n";
                        ref $_ or print "  $_\\n" foreach @INC;
                    }
                }
                else {
                    print "$version$str$Image::ExifTool::RELEASE\\n";
                }
                $helped = 1;
                next;
            }
            if (/^(all|add)?tagsfromfile(=.*)?$/i) {
                $setTagsFile = $2 ? substr( $2, 1 ) : ( @ARGV ? shift : '' );
                if ( $setTagsFile eq '' ) {
                    Error("File must be specified for -tagsFromFile option\\n");
                    $badCmd = 1;
                    next;
                }
                AddSetTagsFile( $setTagsFile,
                    { Replace => ( $1 and lc($1) eq 'add' ) ? 0 : 1 } );
                $tagsFrom = 'File';
                next;
            }
            if ( $a eq '@' ) {
                my $argFile = shift
                  or Error("Expecting filename for -\\@ option\\n"), $badCmd = 1,
                  next;
                if ( $stayOpen == 1 ) {
                    @moreArgs = @ARGV;
                    undef @ARGV;
                }
                elsif ( $stayOpen == 3 ) {
                    if (    $stayOpenFile
                        and $stayOpenFile ne '-'
                        and $argFile eq $stayOpenFile )
                    {
                        $stayOpen = 2;
                        Warn
"Ignoring request to switch to the same -stay_open ARGFILE ($argFile)\\n";
                        next;
                    }
                    close STAYOPEN;
                    $stayOpen = 1;
                }
                my $fp = ( $stayOpen == 1 ? \\*STAYOPEN : \\*ARGFILE );
                unless ( $mt->Open( $fp, $argFile ) ) {
                    unless ($argFile !~ /^\\//
                        and
                        $mt->Open( $fp, "$Image::ExifTool::exeDir/$argFile" ) )
                    {
                        Error "Error opening arg file $argFile\\n";
                        $badCmd = 1;
                        next;
                    }
                }
                if ( $stayOpen == 1 ) {
                    $stayOpenFile = $argFile;
                    $stayOpenBuff = '';
                    $stayOpen     = 2;
                    $helped       = 1;
                    ReadStayOpen( \\@ARGV );
                    next;
                }
                my ( @newArgs, $didBOM );
                foreach (<ARGFILE>) {
                    unless ($didBOM) {
                        s/^\\xef\\xbb\\xbf//;
                        $didBOM = 1;
                    }
                    $_ = FilterArgfileLine($_);
                    push @newArgs, $_ if defined $_;
                }
                close ARGFILE;
                unshift @ARGV, @newArgs;
                next;
            }
            /^(-?)(a|duplicates)$/i
              and $mt->Options( Duplicates => ( $1 ? 0 : 1 ) ), next;
            if ( $a eq 'api' ) {
                my $opt = shift;
                if ( defined $opt and length $opt ) {
                    my $val = ( $opt =~ s/=(.*)//s ) ? $1 : 1;
                    $val = undef unless $opt =~ s/\\^$// or length $val;
                    $mt->Options( $opt => $val );
                }
                else {
                    unless ($pass) {
                        push @nextPass, '-api';
                        push @nextPass, $opt if defined $opt;
                        next;
                    }
                    print "Available API Options:\\n";
                    my $availableOptions = Image::ExifTool::AvailableOptions();
                    $$_[3]
                      or printf( "  %-17s - %s\\n", $$_[0], $$_[2] )
                      foreach @$availableOptions;
                    $helped = 1;
                }
                next;
            }
            /^arg(s|format)$/i and $argFormat = 1, next;
            if (/^(-?)b(inary)?$/i) {
                ( $binaryOutput, $noBinary ) = $1 ? ( undef, 1 ) : ( 1, undef );
                $mt->Options(
                    Binary    => $binaryOutput,
                    NoPDFList => $binaryOutput
                );
                next;
            }
            if (/^c(oordFormat)?$/i) {
                my $fmt = shift;
                $fmt
                  or Error("Expecting coordinate format for -c option\\n"),
                  $badCmd = 1, next;
                $mt->Options( 'CoordFormat', $fmt );
                next;
            }
            if ( $a eq 'charset' ) {
                my $charset =
                  ( @ARGV and $ARGV[0] !~ /^(-|\\xe2\\x88\\x92)/ ) ? shift : undef;
                if ( not $charset ) {
                    unless ($pass) {
                        push @nextPass, '-charset';
                        push @nextPass, $charset if defined $charset;
                        next;
                    }
                    my %charsets;
                    $charsets{$_} = 1
                      foreach values %Image::ExifTool::charsetName;
                    PrintTagList( 'Available character sets',
                        sort keys %charsets );
                    $helped = 1;
                }
                elsif ( $charset !~ s/^(\\w+)=// or lc($1) eq 'exiftool' ) {
                    {
                        local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
                        undef $evalWarning;
                        $mt->Options( Charset => $charset );
                    }
                    if ($evalWarning) {
                        Warn $evalWarning;
                    }
                    else {
                        $setCharset = $mt->Options('Charset');
                    }
                }
                else {
                    my $type = {
                        id3       => 'ID3',
                        iptc      => 'IPTC',
                        exif      => 'EXIF',
                        filename  => 'FileName',
                        photoshop => 'Photoshop',
                        quicktime => 'QuickTime',
                        riff      => 'RIFF'
                    }->{ lc $1 };
                    $type
                      or Warn("Unknown type for -charset option: $1\\n"), next;
                    $mt->Options( "Charset$type" => $charset );
                }
                next;
            }
            /^config$/i
              and Warn("Ignored -config option (not first on command line)\\n"),
              shift, next;
            if (/^(csv|j(son)?)(\\+?=.*)?$/i) {
                my $dbFile = $3;
                my $dbType = lc($1) eq 'csv' ? 'CSV' : 'JSON';
                unless ($dbFile) {
                    if ( $dbType eq 'CSV' ) {
                        $csv = $dbType;
                    }
                    else {
                        $json = 1;
                        $html = $xml = 0;
                        $mt->Options( Duplicates => 1 );
                        require Image::ExifTool::XMP;
                    }
                    next;
                }
                unless ($pass) {
                    @tags
                      and
                      Warn("Tag arguments should come after the -$1= option\\n");
                    push @nextPass, "-$_";
                    push @newValues, { SaveCount => ++$saveCount };
                    $dbSaveCount = $saveCount;
                    $tagsFrom    = 'CSV';
                    next;
                }
                $dbFile =~ s/^(\\+?=)//;
                $dbAdd = 2        if $1 eq '+=';
                $vout  = \\*STDERR if $srcStdin;
                $verbose and print $vout "Reading $dbType file $dbFile\\n";
                my $msg;
                if ( $mt->Open( \\*CSVFILE, $dbFile ) ) {
                    binmode CSVFILE;
                    require Image::ExifTool::Import;
                    if ( $dbType eq 'CSV' ) {
                        $msg = Image::ExifTool::Import::ReadCSV( \\*CSVFILE,
                            \\%database, $forcePrint, $csvDelim );
                    }
                    else {
                        my $chset = $mt->Options('Charset');
                        $msg = Image::ExifTool::Import::ReadJSON( \\*CSVFILE,
                            \\%database, $forcePrint, $chset );
                    }
                    close(CSVFILE);
                }
                else {
                    $msg = "Error opening $dbType file '\${dbFile}'";
                }
                $msg and Warn("$msg\\n");
                $isWriting = 1;
                $csv       = $dbType;
                next;
            }
            if (/^csvdelim$/i) {
                $csvDelim = shift;
                defined $csvDelim
                  or Error("Expecting argument for -csvDelim option\\n"),
                  $badCmd = 1, next;
                $csvDelim =~ /"/
                  and Error("CSV delimiter can not contain a double quote\\n"),
                  $badCmd = 1, next;
                my %unescape =
                  ( 't' => "\\t", 'n' => "\\n", 'r' => "\\r", '\\\\' => '\\\\' );
                $csvDelim =~ s/\\\\(.)/$unescape{$1}||"\\\\$1"/sge;
                $mt->Options( CSVDelim => $csvDelim );
                next;
            }
            if ( /^d$/ or $a eq 'dateformat' ) {
                my $fmt = shift;
                $fmt
                  or Error("Expecting date format for -d option\\n"),
                  $badCmd = 1, next;
                $mt->Options( 'DateFormat', $fmt );
                next;
            }
            ( /^D$/ or $a eq 'decimal' ) and $showTagID = 'D', next;
            if (/^diff$/i) {
                $diff = shift;
                defined $diff
                  or Error("Expecting file name for -$_ option\\n"), $badCmd = 1;
                CleanFilename($diff);
                next;
            }
            /^delete_original(!?)$/i and $deleteOrig = ( $1 ? 2 : 1 ), next;
            /^list_dir$/i and $listDir = 1, next;
            ( /^e$/ or $a eq '-composite' )
              and $mt->Options( Composite => 0 ), next;
            ( /^-e$/ or $a eq 'composite' )
              and $mt->Options( Composite => 1 ), next;
            ( /^E$/ or $a eq 'escapehtml' or $a eq 'eh' )
              and require Image::ExifTool::HTML
              and $escapeHTML = 1, next;
            ( $a eq 'ec' or $a eq 'escapec' )   and $escapeC   = 1, next;
            ( $a eq 'ex' or $a eq 'escapexml' ) and $escapeXML = 1, next;

            if (/^echo(\\d)?$/i) {
                my $n   = $1 || 1;
                my $arg = shift;
                next unless defined $arg;
                $n > 4 and Warn("Invalid -echo number\\n"), next;
                if ( $n > 2 ) {
                    $n == 3 ? push( @echo3, $arg ) : push( @echo4, $arg );
                }
                else {
                    print { $n == 2 ? \\*STDERR : \\*STDOUT } $arg, "\\n";
                }
                $helped = 1;
                next;
            }
            if (/^(ee|extractembedded)(\\d*)$/i) {
                $mt->Options( ExtractEmbedded => $2 || 1 );
                $mt->Options( Duplicates      => 1 );
                next;
            }
            if (/^efile(\\d+)?(!)?$/i) {
                my $arg = shift;
                defined $arg
                  or Error("Expecting file name for -$_ option\\n"),
                  $badCmd = 1, next;
                $efile[0] = $arg if not $1 or $1 & 0x01;
                $efile[1] = $arg if $1 and $1 & 0x02;
                $efile[2] = $arg if $1 and $1 & 0x04;
                $efile[3] = $arg if $1 and $1 & 0x08;
                $efile[4] = $arg if $1 and $1 & 0x016;
                unlink $arg if $2;
                next;
            }
            if (/^-?ext(ension)?(\\+)?$/i) {
                my $ext = shift;
                defined $ext
                  or Error("Expecting extension for -ext option\\n"),
                  $badCmd = 1, next;
                my $flag = /^-/ ? 0 : ( $2 ? 2 : 1 );
                $filterFlag |= ( 0x01 << $flag );
                $ext =~ s/^\\.//;
                $filterExt{ uc($ext) } = $flag ? 1 : 0;
                next;
            }
            if ( /^f$/ or $a eq 'forceprint' ) {
                $forcePrint = 1;
                next;
            }
            if ( /^F([-+]?\\d*)$/ or /^fixbase([-+]?\\d*)$/i ) {
                $mt->Options( FixBase => $1 );
                next;
            }
            if (/^fast(\\d*)$/i) {
                $mt->Options( FastScan => ( length $1 ? $1 : 1 ) );
                next;
            }
            if (/^(file\\d+)$/i) {
                $altFile{ lc $1 } = shift
                  or Error("Expecting file name for -file option\\n"),
                  $badCmd = 1, next;
                next;
            }
            if (/^fileorder(\\d*)$/i) {
                push @fileOrder, shift if @ARGV;
                my $num = $1 || 0;
                $fileOrderFast = $num
                  if not defined $fileOrderFast or $fileOrderFast > $num;
                next;
            }
            $a eq 'globaltimeshift'
              and $mt->Options( GlobalTimeShift => shift ), next;
            if (/^(g)(roupHeadings|roupNames)?([\\d:]*)$/i) {
                $showGroup = $3 || 0;
                $allGroup  = ( $2 ? lc($2) eq 'roupnames' : $1 eq 'G' );
                $mt->Options( SavePath   => 1 ) if $showGroup =~ /\\b5\\b/;
                $mt->Options( SaveFormat => 1 ) if $showGroup =~ /\\b6\\b/;
                next;
            }
            if ( $a eq 'geotag' ) {
                my $trkfile = shift;
                unless ($pass) {
                    push @nextPass, '-geotag', $trkfile;
                    next;
                }
                $trkfile
                  or Error("Expecting file name for -geotag option\\n"),
                  $badCmd = 1, next;
                if ( HasWildcards($trkfile) ) {
                    my @trks;
                    if ( $^O eq 'MSWin32' and eval { require Win32::FindFile } )
                    {
                        @trks = FindFileWindows( $mt, $trkfile );
                    }
                    elsif ( eval { require File::Glob } ) {
                        @trks = File::Glob::bsd_glob($trkfile);
                    }
                    else {
                        @trks = glob($trkfile);
                    }
                    @trks
                      or Error("No matching file found for -geotag option\\n"),
                      $badCmd = 1, next;
                    push @newValues, 'geotag=' . shift(@trks) while @trks > 1;
                    $trkfile = pop(@trks);
                }
                $_ = "geotag=$trkfile";
            }
            if ( /^h$/ or $a eq 'htmlformat' ) {
                require Image::ExifTool::HTML;
                $html = $escapeHTML = 1;
                $json = $xml        = 0;
                next;
            }
            ( /^H$/ or $a eq 'hex' ) and $showTagID = 'H', next;
            if (/^htmldump([-+]?\\d+)?$/i) {
                $verbose = ( $verbose || 0 ) + 1;
                $html    = 2;
                $mt->Options( HtmlDumpBase => $1 ) if defined $1;
                next;
            }
            if (/^i(gnore)?$/i) {
                my $dir = shift;
                defined $dir
                  or Error("Expecting directory name for -i option\\n"),
                  $badCmd = 1, next;
                $ignore{$dir} = 1;
                $dir eq 'HIDDEN' and $ignoreHidden = 1;
                next;
            }
            if (/^if(\\d*)$/i) {
                my $cond = shift;
                my $fast = length($1) ? $1 : undef;
                defined $cond
                  or Error("Expecting expression for -if option\\n"),
                  $badCmd = 1, next;
                if (   not @condition
                    or not defined $fast
                    or ( defined $fastCondition and $fastCondition > $fast ) )
                {
                    $fastCondition = $fast;
                }
                $cond =~ /^\\s*(not\\s*)\\$ok\\s*$/i
                  and ( $1 xor $rtnValPrev )
                  and $failCondition = 1;
                push @requestTags,
                  $cond =~ /\\$\\{?((?:[-_0-9A-Z]+:)*[-_0-9A-Z?*]+)/ig;
                push @condition, $cond;
                next;
            }
            /^(k|pause)$/i and $pause = 1, next;
            ( /^l$/ or $a eq 'long' ) and --$outFormat, next;
            ( /^L$/ or $a eq 'latin' )
              and $mt->Options( Charset => 'Latin' ), next;
            if ( $a eq 'lang' ) {
                $langOpt =
                  ( @ARGV and $ARGV[0] !~ /^(-|\\xe2\\x88\\x92)/ ) ? shift : undef;
                if ($langOpt) {
                    $langOpt =~ tr/-A-Z/_a-z/;
                    $mt->Options( Lang => $langOpt );
                    next if $langOpt eq $mt->Options('Lang');
                }
                elsif ( not $pass ) {
                    push @nextPass, '-lang';
                    push @nextPass, $langOpt if defined $langOpt;
                    next;
                }
                my $langs = $quiet ? '' : "Available languages:\\n";
                $langs .= "  $_ - $Image::ExifTool::langName{$_}\\n"
                  foreach @Image::ExifTool::langs;
                $langs =~ tr/_/-/;
                $langs = Image::ExifTool::HTML::EscapeHTML($langs)
                  if $escapeHTML;
                $langs = $mt->Decode( $langs, 'UTF8' );
                $langOpt
                  and Error(
                    "Invalid or unsupported language '\${langOpt}'.\\n$langs"),
                  $badCmd = 1, next;
                print $langs;
                $helped = 1;
                next;
            }
            if ( $a eq 'listitem' ) {
                my $li = shift;
                defined $li and Image::ExifTool::IsInt($li)
                  or Warn("Expecting integer for -listItem option\\n"), next;
                $mt->Options( ListItem => $li );
                $listItem = $li;
                next;
            }
            /^(m|ignoreminorerrors)$/i
              and $mt->Options( IgnoreMinorErrors => 1 ), next;
            /^(n|-printconv)$/i and $mt->Options( PrintConv => 0 ), next;
            /^(-n|printconv)$/i and $mt->Options( PrintConv => 1 ), next;
            $a eq 'nop'         and $helped = 1, next;
            if (/^o(ut)?$/i) {
                $outOpt = shift;
                defined $outOpt
                  or Error(
                    "Expected output file or directory name for -o option\\n"),
                  $badCmd = 1, next;
                CleanFilename($outOpt);
                $vout = \\*STDERR if $vout =~ /^-(\\.\\w+)?$/;
                next;
            }
            $a eq 'overwrite_original'          and $overwriteOrig = 1, next;
            $a eq 'overwrite_original_in_place' and $overwriteOrig = 2, next;
            $a eq 'plot'
              and require Image::ExifTool::Plot
              and $plot = Image::ExifTool::Plot->new, next;
            if ( /^p(-?)$/ or /^printformat(-?)$/i ) {
                my $fmt = shift;
                if ($pass) {
                    LoadPrintFormat( $fmt, $1 || $binaryOutput );
                    if ( not $useMWG
                        and grep /^([-_0-9A-Z]+:)*1?mwg:/i, @requestTags )
                    {
                        $useMWG = 1;
                        require Image::ExifTool::MWG;
                        Image::ExifTool::MWG::Load();
                    }
                }
                else {
                    push @nextPass, "-$_", $fmt;
                }
                next;
            }
            ( /^P$/ or $a eq 'preserve' ) and $preserveTime = 1, next;
            $a eq 'password' and $mt->Options( Password => shift ), next;
            if (/^progress(\\d*)(:.*)?$/i) {
                $progressIncr = $1 || 1;
                $progressNext = 0;
                if ($2) {
                    $windowTitle = substr $2, 1;
                    $windowTitle = 'ExifTool %p%%' unless length $windowTitle;
                    $windowTitle =~ /%\\d*[bpr]/ and $progress = 0
                      unless defined $progress;
                }
                else {
                    $progress = 1;
                    $verbose  = 0 unless defined $verbose;
                }
                $progressCount = 0;
                next;
            }
            /^purge(\\d*)$/i
              and $purge = $1 || 1, Image::ExifTool::Purge($purge), next;
            /^q(uiet)?$/i and ++$quiet, next;
            /^r(ecurse)?(\\.?)$/i and $recurse = ( $2 ? 2 : 1 ), next;
            if ( $a eq 'require' ) {
                my $ver = shift;
                unless ( defined $ver and Image::ExifTool::IsFloat($ver) ) {
                    Error("Expecting version number for -require option\\n");
                    $badCmd = 1;
                    next;
                }
                unless ( $Image::ExifTool::VERSION >= $ver ) {
                    Error("Requires ExifTool version $ver or later\\n");
                    $badCmd = 1;
                }
                next;
            }
            $a eq 'restore_original' and $deleteOrig = 0, next;
            ( /^S$/ or $a eq 'veryshort' ) and $outFormat += 2, next;
            /^s(hort)?(\\d*)$/i
              and $outFormat = $2 eq '' ? $outFormat + 1 : $2, next;
            $a eq 'scanforxmp' and $mt->Options( ScanForXMP => 1 ), next;
            if (/^sep(arator)?$/i) {
                my $sep = $listSep = shift;
                defined $listSep
                  or Error("Expecting list item separator for -sep option\\n"),
                  $badCmd = 1, next;
                $sep =~ s/\\\\(.)/$unescapeChar{$1}||$1/sge;
                ( defined $binSep ? $binTerm : $binSep ) = $sep;
                $mt->Options( ListSep => $listSep );
                $joinLists = 1;
                my $listSplit = quotemeta $listSep;
                $listSplit =~ s/(\\\\ )+/\\\\s\\*/g;
                $listSplit = '\\\\s+' if $listSplit eq '\\\\s*';
                $mt->Options( ListSplit => $listSplit );
                next;
            }
            /^(-)?sort$/i and $sortOpt = $1 ? 0 : 1, next;
            if ( $a eq 'srcfile' ) {
                @ARGV or Warn("Expecting FMT for -srcfile option\\n"), next;
                push @srcFmt, shift;
                next;
            }
            if ( $a eq 'stay_open' ) {
                my $arg = shift;
                defined $arg
                  or Warn("Expecting argument for -stay_open option\\n"), next;
                if ( $arg =~ /^(1|true)$/i ) {
                    if ( not $stayOpen ) {
                        $stayOpen = 1;
                    }
                    elsif ( $stayOpen == 2 ) {
                        $stayOpen = 3;
                    }
                    else {
                        Warn "-stay_open already active\\n";
                    }
                }
                elsif ( $arg =~ /^(0|false)$/i ) {
                    if ( $stayOpen >= 2 ) {
                        close STAYOPEN;
                        push @ARGV, @moreArgs;
                        undef @moreArgs;
                    }
                    elsif ( not $stayOpen ) {
                        Warn("-stay_open wasn't active\\n");
                    }
                    $stayOpen = 0;
                }
                else {
                    Warn "Invalid argument for -stay_open\\n";
                }
                next;
            }
            if (/^(-)?struct$/i) {
                $mt->Options( Struct => $1 ? 0 : 1 );
                next;
            }
            /^t(ab)?$/ and $tabFormat = 1, next;
            if ( /^T$/ or $a eq 'table' ) {
                $tabFormat = $forcePrint = 1;
                $outFormat += 2;
                ++$quiet;
                next;
            }
            if (/^(u)(nknown(2)?)?$/i) {
                my $inc = ( $3 or ( not $2 and $1 eq 'U' ) ) ? 2 : 1;
                $mt->Options( Unknown => $mt->Options('Unknown') + $inc );
                next;
            }
            if ( $a eq 'use' ) {
                my $module = shift;
                $module
                  or Error("Expecting module name for -use option\\n"),
                  $badCmd = 1, next;
                lc $module eq 'mwg' and $useMWG = 1, next;
                $module =~ /[^\\w:]/
                  and Error("Invalid module name: $module\\n"), $badCmd = 1,
                  next;
                local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
                unless ( eval "require Image::ExifTool::$module"
                    or eval "require $module"
                    or eval "require '\${module}'" )
                {
                    Error("Error using module $module\\n");
                    $badCmd = 1;
                }
                next;
            }
            if ( $a eq 'userparam' ) {
                my $opt = shift;
                defined $opt
                  or Error("Expected parameter for -userParam option\\n"),
                  $badCmd = 1, next;
                $opt =~ /=/ or $opt .= '=1';
                $mt->Options( UserParam => $opt );
                next;
            }
            if (/^v(erbose)?(\\d*)$/i) {
                $verbose = ( $2 eq '' ) ? ( $verbose || 0 ) + 1 : $2;
                next;
            }
            if (/^(w|textout|tagout)([!+]*)$/i) {
                $textOut = shift || Warn("Expecting argument for -$_ option\\n");
                my ( $t1, $t2 ) = ( $1, $2 );
                $textOverwrite = 0;
                $textOverwrite += 1 if $t2 =~ /!/;
                $textOverwrite += 2 if $t2 =~ /\\+/;
                if ( $t1 ne 'W' and lc($t1) ne 'tagout' ) {
                    undef $tagOut;
                }
                elsif ( $textOverwrite >= 2
                    and $textOut !~ /%[-+]?\\d*[.:]?\\d*[lu]?[tgso]/ )
                {
                    $tagOut = 0;
                }
                else {
                    $tagOut = 1;
                }
                next;
            }
            if (/^(-?)(wext|tagoutext)$/i) {
                my $ext = shift;
                defined $ext
                  or Error("Expecting extension for -wext option\\n"),
                  $badCmd = 1, next;
                my $flag = 1;
                $1 and $wext{'*'} = 1, $flag = -1;
                $ext =~ s/^\\.//;
                $wext{ lc $ext } = $flag;
                next;
            }
            if ( $a eq 'wm' or $a eq 'writemode' ) {
                my $wm = shift;
                defined $wm
                  or Error("Expecting argument for -$_ option\\n"), $badCmd = 1,
                  next;
                $wm =~ /^[wcg]*$/i
                  or Error("Invalid argument for -$_ option\\n"), $badCmd = 1,
                  next;
                $mt->Options( WriteMode => $wm );
                next;
            }
            if ( /^x$/ or $a eq 'exclude' ) {
                my $tag = shift;
                defined $tag
                  or Error("Expecting tag name for -x option\\n"), $badCmd = 1,
                  next;
                $tag =~ s/\\ball\\b/\\*/ig;
                if ( not $tagsFrom ) {
                    push @exclude, $tag;
                }
                elsif ( $tagsFrom eq 'CSV' ) {
                    push @csvExclude, $tag;
                }
                else {
                    push @{ $setTags{$setTagsFile} }, "-$tag";
                }
                next;
            }
            ( /^X$/ or $a eq 'xmlformat' )
              and $xml = 1, $html = $json = 0, $mt->Options( Duplicates => 1 ),
              next;
            if ( $a eq 'php' ) {
                $json = 2;
                $html = $xml = 0;
                $mt->Options( Duplicates => 1 );
                next;
            }
            if (/^z(ip)?$/i) {
                $doUnzip = 1;
                $mt->Options( Compress => 1, XMPShorthand => 1 );
                $mt->Options( Compact  => 1 ) unless $mt->Options('Compact');
                next;
            }
            $_ eq '' and push( @files, '-' ), $srcStdin = 1, next;
            length $_ eq 1
              and $_ ne '*'
              and Error("Unknown option -$_\\n"), $badCmd = 1, next;
            if (/^[^<]+(<?)=(.*)/s) {
                my $val = $2;
                if (    $1
                    and length($val)
                    and ( $val eq '@' or not defined FilenameSPrintf($val) ) )
                {
                    push @newValues, { SaveCount => ++$saveCount };
                }
                push @newValues, $_;
                if (/^([-_0-9A-Z]+:)*1?mwg:/i) {
                    $useMWG = 1;
                }
                elsif (/^([-_0-9A-Z]+:)*(filename|directory|testname)\\b/i) {
                    $doSetFileName = 1;
                }
                elsif (/^([-_0-9A-Z]+:)*(geotag|geotime|geosync|geolocate)\\b/i)
                {
                    if ( lc $2 eq 'geotime' ) {
                        $addGeotime = '';
                    }
                    else {
                        unshift @newValues, pop @newValues;
                        if (    lc $2 eq 'geotag'
                            and ( not defined $addGeotime or $addGeotime )
                            and length $val )
                        {
                            $addGeotime = ( $1 || '' )
                              . q[Geotime<\${DateTimeOriginal#;$_=$self->GetValue('SubSecDateTimeOriginal','ValueConv') || $_}];
                        }
                    }
                }
            }
            else {
                if ( not $setTagsFile and $tagsFrom ne 'CSV' and /(<|>)/ ) {
                    AddSetTagsFile( $setTagsFile = '@' );
                    $tagsFrom = 'File';
                }
                if ( $tagsFrom eq 'CSV' ) {
                    my $lst = s/^-// ? \\@csvExclude : \\@tags;
                    push @$lst, $_;
                }
                elsif ($setTagsFile) {
                    push @{ $setTags{$setTagsFile} }, $_;
                    if ( $1 eq '>' ) {
                        $useMWG = 1 if /^(.*>\\s*)?([-_0-9A-Z]+:)*1?mwg:/si;
                        if (/\\b(filename|directory|testname)#?$/i) {
                            $doSetFileName = 1;
                        }
                        elsif (/\\bgeotime#?$/i) {
                            $addGeotime = '';
                        }
                    }
                    else {
                        $useMWG = 1
                          if /^([^<]+<\\s*(.*\\$\\{?)?)?([-_0-9A-Z]+:)*1?mwg:/si;
                        if (/^([-_0-9A-Z]+:)*(filename|directory|testname)\\b/i)
                        {
                            $doSetFileName = 1;
                        }
                        elsif (/^([-_0-9A-Z]+:)*geotime\\b/i) {
                            $addGeotime = '';
                        }
                    }
                }
                else {
                    my $lst = s/^-// ? \\@exclude : \\@tags;
                    Warn(qq(Invalid TAG name: "$_"\\n))
                      unless /^([-_0-9A-Z*]+:)*([-_0-9A-Z*?]+)#?$/i;
                    push @$lst, $_;
                }
            }
        }
        else {
            unless ($pass) {
                push @nextPass, $_;
                next;
            }
            if ( $doGlob and HasWildcards($_) ) {
                if ( $^O eq 'MSWin32' and eval { require Win32::FindFile } ) {
                    push @files, FindFileWindows( $mt, $_ );
                }
                else {
                    push @files, File::Glob::bsd_glob($_);
                }
                $doGlob = 2;
            }
            else {
                push @files, $_;
                $srcStdin = 1 if $_ eq '-';
            }
        }
    }

    $mt->Options( UserParam => 'OK=' . ( not $rtnValPrev ) );

    $vout = \\*STDERR if $srcStdin and ( $isWriting or @newValues );
    $mt->Options( TextOut => $vout ) if $vout eq \\*STDERR;

    if ( $useMWG and not defined $mt->Options('CharsetEXIF') ) {
        $mt->Options( CharsetEXIF => 'UTF8' );
    }

    if ( not @files and not $outOpt and not @newValues ) {
        my $loc = $mt->Options('Geolocation');
        $loc and $loc ne '1' and push( @files, qq(\\@JSON:{}) ), $geoOnly = 1;
    }

    unless ( ( @tags and not $outOpt ) or @files or @newValues or $geoOnly ) {
        if ( $doGlob and $doGlob == 2 ) {
            Error "No matching files\\n";
            next;
        }
        $outOpt and Error("Nothing to write\\n"), next;
        unless ($helped) {
            local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
            my $dummy = \\*SAVEERR;
            unless ( $^O eq 'os2' ) {
                open SAVEERR, ">&STDERR";
                open STDERR,  '>/dev/null';
            }
            if ( system( 'perldoc', $0 ) ) {
                print "Syntax:  exiftool [OPTIONS] FILE\\n\\n";
                print
"Consult the exiftool documentation for a full list of options.\\n";
            }
            unless ( $^O eq 'os2' ) {
                close STDERR;
                open STDERR, '>&SAVEERR';
            }
        }
        next;
    }

    if ( defined $deleteOrig and ( @newValues or @tags ) ) {
        if ( not @newValues ) {
            my $verb = $deleteOrig ? 'deleting' : 'restoring from';
            Error "Can't specify tags when $verb originals\\n";
        }
        elsif ($deleteOrig) {
            Error "Can't use -delete_original when writing.\\n";
            Error "Maybe you meant -overwrite_original ?\\n";
        }
        else {
            Error "It makes no sense to use -restore_original when writing\\n";
        }
        next;
    }

    if ( $overwriteOrig > 1 and $outOpt ) {
        Error "Can't overwrite in place when -o option is used\\n";
        next;
    }

    if (
        ( $tagOut or defined $diff )
        and (  $csv
            or $json
            or %printFmt
            or $tabFormat
            or $xml
            or $plot
            or ( $verbose and $html ) )
      )
    {
        my $opt = $tagOut ? '-W' : '-diff';
        Error
"Sorry, $opt may not be combined with -csv, -htmlDump, -j, -p, -t or -X\\n";
        next;
    }

    if ( $csv and $csv eq 'CSV' and not $isWriting ) {
        $json = 0;
        if ($textOut) {
            $textOut2 = $textOut;
            undef $textOut;
        }
        if ($binaryOutput) {
            $binaryOutput = 0;
            $setCharset   = 'default' unless defined $setCharset;
        }
        if (%printFmt) {
            Warn "The -csv option has no effect when -p is used\\n";
            undef $csv;
        }
        require Image::ExifTool::XMP if $setCharset;
    }
    if ( $plot and $textOut ) {
        $textOut2 = $textOut;
        undef $textOut;
    }
    if ($textOut2) {
        if ( $textOverwrite > 1 ) {
            Error "Can not append to multi-file output format\\n";
            undef $textOut2;
            next;
        }
        if ( not $textOverwrite and $mt->Exists( $textOut2, 1 ) ) {
            Error "Output file $textOut2 already exists\\n";
            undef $textOut2;
            next;
        }
        CreateDirectory($textOut2);
        if ( $mt->Open( \\*OUTFILE, $textOut2, '>' ) ) {
            close( \\*OUTFILE );
            unlink($textOut2);
        }
        else {
            Error("Error creating $textOut2\\n");
            undef $textOut2;
            next;
        }
    }

    if ( $escapeHTML or $json ) {
        $mt->Options( Charset => 'UTF8' ) if $json;
        $mt->Options( Escape => 'HTML' ) if $escapeHTML and not $xml;
    }
    elsif ( $escapeXML and not $xml ) {
        $mt->Options( Escape => 'XML' );
    }

    if ($sortOpt) {
        my $sort =
          ( $outFormat > 0 or $xml or $json or $csv or $plot )
          ? 'Tag'
          : 'Descr';
        $mt->Options( Sort => $sort, Sort2 => $sort );
    }

    if ( $mt->Options('Struct') and not $structOpt ) {
        $structOpt = $mt->Options('Struct');
        require 'Image/ExifTool/XMPStruct.pl';
    }

    if ($plot) {
        undef $joinLists;
        $mt->Options( List => 1 );
        $plot->Settings( $mt->Options('Plot') );
    }
    elsif ($xml) {
        require Image::ExifTool::XMP;
        my $charset = $mt->Options('Charset');
        my %encoding = (
            UTF8     => 'UTF-8',
            Latin    => 'windows-1252',
            Latin2   => 'windows-1250',
            Cyrillic => 'windows-1251',
            Greek    => 'windows-1253',
            Turkish  => 'windows-1254',
            Hebrew   => 'windows-1255',
            Arabic   => 'windows-1256',
            Baltic   => 'windows-1257',
            Vietnam  => 'windows-1258',
            MacRoman => 'macintosh',
        );
        unless ( $encoding{$charset} ) {
            $charset = 'UTF8';
            $mt->Options( Charset => $charset );
        }
        $fileHeader = "<?xml version='1.0' encoding='$encoding{$charset}'?>\\n"
          . "<rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>\\n";
        $fileTrailer = "</rdf:RDF>\\n";
        $joinLists = 1 if $outFormat > 0;
        $mt->Options( List => 1 ) unless $joinLists;
        $showGroup = $allGroup = 1;

        $binaryOutput = ( $outFormat > 0 ? undef : 0 ) if $binaryOutput;
        $showTagID    = 'D' if $tabFormat and not $showTagID;
    }
    elsif ($json) {
        if ( $json == 1 ) {
            $fileHeader  = '[';
            $fileTrailer = "]\\n";
        }
        else {
            $fileHeader  = 'Array(';
            $fileTrailer = ");\\n";
        }
        if ($binaryOutput) {
            $binaryOutput = 0;
            require Image::ExifTool::XMP if $json == 1;
        }
        $mt->Options( List => 1 ) unless $joinLists;
        $showTagID = 'D' if $tabFormat and not $showTagID;
    }
    elsif ($structOpt) {
        $mt->Options( List => 1 );
    }
    else {
        $joinLists = 1;
    }

    if ($argFormat) {
        $outFormat = 3;
        $allGroup  = 1 if defined $showGroup;
    }

    if ( Image::ExifTool::IsPC() ) {
        tr/\\\\/\\// foreach @files;
    }

    unless (@files) {
        unless ($outOpt) {
            if ( $doGlob and $doGlob == 2 ) {
                Error "No matching files\\n";
            }
            else {
                Error "No file specified\\n";
            }
            next;
        }
        push @files, '';
    }

    if ($verbose) {
        $disableOutput = 1  unless @tags or @exclude or $tagOut;
        undef $binaryOutput unless $tagOut;
        if ($html) {
            $html = 2;
            $mt->Options( HtmlDump => $verbose );
        }
        else {
            $mt->Options( Verbose => $verbose ) unless $tagOut;
        }
    }
    elsif ( defined $verbose ) {
        require FileHandle;
        STDOUT->autoflush(1);
        STDERR->autoflush(1);
    }

    my $needSave = 1;
    if (@newValues) {
        if ($addGeotime) {
            AddSetTagsFile( $setTagsFile = '@' )
              unless $setTagsFile and $setTagsFile eq '@';
            push @{ $setTags{$setTagsFile} }, $addGeotime;
            $verbose and print $vout qq(Using default "-$addGeotime"\\n);
        }
        my %setTagsIndex;
        my %addDelOpt = (
            '+'            => 'AddValue',
            '-'            => 'DelValue',
            "\\xe2\\x88\\x92" => 'DelValue'
        );
        $saveCount = 0;
        foreach (@newValues) {
            if ( ref $_ eq 'HASH' ) {
                if ( $$_{SaveCount} ) {
                    $saveCount = $mt->SaveNewValues();
                    $needSave  = 0;
                    push @dynamicFiles, \\$csv if $$_{SaveCount} == $dbSaveCount;
                }
                next;
            }
            /(.*?)=(.*)/s or next;
            my ( $tag, $newVal ) = ( $1, $2 );
            $tag =~ s/\\ball\\b/\\*/ig;
            $newVal eq '' and undef $newVal unless $tag =~ s/\\^([-+]*)$/$1/;
            if ( $tag =~ /^(All)?TagsFromFile$/i ) {
                defined $newVal
                  or Error("Need file name for -tagsFromFile\\n"), next Command;
                ++$isWriting;
                if (
                       $newVal eq '@'
                    or not defined FilenameSPrintf($newVal)
                    or
                    grep /\\bfile\\d+:/i, @{ $setTags{$newVal} }
                  )
                {
                    push @dynamicFiles, $newVal;
                    next;
                }
                unless ( $mt->Exists($newVal) or $newVal eq '-' ) {
                    Error
"File '\${newVal}' does not exist for -tagsFromFile option\\n";
                    next Command;
                }
                my $setTags = $setTags{$newVal};
                if ( $setTagsList{$newVal} ) {
                    my $i = $setTagsIndex{$newVal} || 0;
                    $setTagsIndex{$newVal} = $i + 1;
                    $setTags = $setTagsList{$newVal}[$i]
                      if $setTagsList{$newVal}[$i];
                }
                unless ( DoSetFromFile( $mt, $newVal, $setTags ) ) {
                    $rtnVal = 1;
                    next Command;
                }
                $needSave = 1;
                next;
            }
            my %opts = ( Shift => 0 );

            $opts{Protected} = 1 unless $tag =~ /[?*]/;

            if ( $tag =~ s/<// and defined $newVal ) {
                if ( defined FilenameSPrintf($newVal) ) {
                    SlurpFile( $newVal, \\$newVal ) or next;
                }
                else {
                    $tag =~ s/([-+]|\\xe2\\x88\\x92)$//
                      and $opts{ $addDelOpt{$1} } = 1;
                    my $result = Image::ExifTool::IsWritable($tag);
                    if ($result) {
                        $opts{ProtectSaved} = $saveCount;

                        push @dynamicFiles, [ $tag, $newVal, \\%opts ];
                        ++$isWriting;
                    }
                    elsif ( defined $result ) {
                        Warn "Tag '\${tag}' is not writable\\n";
                    }
                    else {
                        Warn "Tag '\${tag}' does not exist\\n";
                    }
                    next;
                }
            }
            if ( $tag =~ s/([-+]|\\xe2\\x88\\x92)$// ) {
                $opts{ $addDelOpt{$1} } = 1;

                $newVal = '' if $1 eq '-' and not defined $newVal;
            }
            if ( $escapeC and defined $newVal ) {
                $newVal =~
s/\\\\(x([0-9a-fA-F]{2})|.)/$2 ? chr(hex($2)) : $unescC{$1} || $1/seg;
            }
            my ( $rtn, $wrn ) = $mt->SetNewValue( $tag, $newVal, %opts );
            $needSave = 1;
            ++$isWriting if $rtn;
            $wrn and Warning( $mt, $wrn );
        }
        foreach (@exclude) {
            $mt->SetNewValue( $_, undef, Replace => 2 );
            $needSave = 1;
        }
        unless ( $isWriting or $outOpt or @tags ) {
            Error "Nothing to do.\\n";
            next;
        }
    }
    elsif ( grep /^(\\*:)?\\*$/, @exclude ) {
        Error "All tags excluded -- nothing to do.\\n";
        next;
    }
    if ($isWriting) {
        if ( defined $diff ) {
            Error "Can't use -diff option when writing tags\\n";
            next;
        }
        elsif ($plot) {
            Error "Can't use -plot option when writing tags\\n";
            next;
        }
        elsif ( @tags and not $outOpt and not $csv ) {
            my ( $tg, $s ) =
              @tags > 1 ? ( "$tags[0] ...", 's' ) : ( $tags[0], '' );
            Warn "Ignored superfluous tag name$s or invalid option$s: -$tg\\n";
        }
    }
    $mt->SaveNewValues() if $outOpt or ( @dynamicFiles and $needSave );

    $multiFile = 1 if @files > 1;
    @exclude and $mt->Options( Exclude => \\@exclude );

    undef $binaryOutput if $html;

    if ($binaryOutput) {
        $outFormat = 99;
        $mt->Options( PrintConv => 0 );
        unless ( $textOut or $binaryStdout ) {
            binmode(STDOUT);
            $binaryStdout = 1;
            $mt->Options( TextOut => ( $vout = \\*STDERR ) );
        }
        undef $showGroup;
    }

    if (    defined $showGroup
        and not( @tags and ( $allGroup or $csv ) )
        and ( $sortOpt or not defined $sortOpt ) )
    {
        $mt->Options( Sort => "Group$showGroup" );
    }

    if ($textOut) {
        CleanFilename($textOut);

        $textOut = ".$textOut" unless $textOut =~ /[.%]/ or defined $tagOut;
    }

    if ($outOpt) {
        my $type = GetFileType($outOpt);
        if ($type) {
            my $canWrite = CanWrite($outOpt);
            unless ($canWrite) {
                if ( defined $canWrite and $canWrite eq '' ) {
                    $type = Image::ExifTool::GetFileExtension($outOpt);
                    $type = uc($outOpt) unless defined $type;
                }
                Error "Can't write $type files\\n";
                next;
            }
            $scanWritable = $type unless CanCreate($type);
        }
        else {
            $scanWritable = 1;
        }
        $isWriting = 1;
    }
    elsif ( $isWriting or defined $deleteOrig ) {
        $scanWritable = 1;
    }

    $altEnc = $mt->Options('Charset');
    undef $altEnc if $altEnc eq 'UTF8';

    if ( not $altEnc and $mt->Options('Lang') ne 'en' ) {
        $fixLen = eval { require Unicode::GCString } ? 2 : 1;
    }

    if (@fileOrder) {
        my @allFiles;
        ProcessFiles( $mt, \\@allFiles );
        my $sortTool = Image::ExifTool->new;
        $sortTool->Options( FastScan   => $fileOrderFast ) if $fileOrderFast;
        $sortTool->Options( PrintConv  => $mt->Options('PrintConv') );
        $sortTool->Options( Duplicates => 0 );
        my ( %sortBy, %isFloat, @rev, $file );
        push @rev, ( s/^-// ? 1 : 0 ) foreach @fileOrder;

        foreach $file (@allFiles) {
            my @tags;
            my $info =
              $sortTool->ImageInfo( Infile( $file, 1 ), @fileOrder, \\@tags );
            foreach (@tags) {
                $_ = $$info{$_};
                defined $_ or $_ = '~', next;
                $isFloat{$_} = Image::ExifTool::IsFloat($_);
                s/(\\d+)/(length($1) < 12 ? '0'x(12-length($1)) : '') . $1/eg
                  unless $isFloat{$_};
            }
            $sortBy{$file} = \\@tags;
        }
        @files = sort {
            my ( $i, $cmp );
            for ( $i = 0 ; $i < @rev ; ++$i ) {
                my $u = $sortBy{$a}[$i];
                my $v = $sortBy{$b}[$i];
                if ( not $isFloat{$u} and not $isFloat{$v} ) {
                    $cmp = $u cmp $v;
                }
                elsif ( $isFloat{$u} and $isFloat{$v} ) {
                    $cmp = $u <=> $v;
                }
                else {
                    $cmp = $isFloat{$u} ? -1 : 1;
                }
                return $rev[$i] ? -$cmp : $cmp if $cmp;
            }
            return $a cmp $b;
        } @allFiles;
    }
    elsif ( defined $progress ) {
        my @allFiles;
        ProcessFiles( $mt, \\@allFiles );
        @files = @allFiles;
    }
    $progressMax = scalar @files if defined $progress;

    my @dbKeys = keys %database;
    if (@dbKeys) {
        if ( eval { require Cwd } ) {
            undef $evalWarning;
            local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
            foreach (@dbKeys) {
                my $db = $database{$_};
                tr/\\\\/\\// and $database{$_} = $db;
                $database{lc} = $db unless $database{lc};

                my $absPath = AbsPath($_);
                if ( defined $absPath ) {
                    $database{$absPath} = $db unless $database{$absPath};
                    if ( $verbose and $verbose > 1 ) {
                        print $vout
"Imported entry for '\${_}' (full path: '\${absPath}')\\n";
                    }
                    $database{ lc $absPath } = $db
                      unless $database{ lc $absPath };
                }
                elsif ( $verbose and $verbose > 1 ) {
                    print $vout "Imported entry for '\${_}' (no full path)\\n";
                }
            }
        }
    }

    ProcessFiles($mt);

    Error "No file with specified extension\\n" if $filtered and not $validFile;

    if ($textOut) {
        foreach ( keys %outTrailer ) {
            next unless $outTrailer{$_};
            if ( $mt->Open( \\*OUTTRAIL, $_, '>>' ) ) {
                my $fp = \\*OUTTRAIL;
                print $fp $outTrailer{$_};
                close $fp;
            }
            else {
                Error("Error appending to $_\\n");
            }
        }
    }
    else {
        print $sectTrailer if $sectTrailer;
        print $fileTrailer if $fileTrailer and not $fileHeader;
        my ( $fp, $err );
        if ($textOut2) {
            if ( $mt->Open( \\*OUTFILE, $textOut2, '>' ) ) {
                $fp = \\*OUTFILE;
            }
            else {
                Error("Error creating $textOut2\\n");
                $err = 1;
            }
        }
        unless ($err) {
            PrintCSV($fp) if $csv and not $isWriting;
            if ($plot) {
                $plot->Draw( $fp || \\*STDOUT );
                if ( $$plot{Error} ) {
                    Error("Error: $$plot{Error}\\n");
                    $err = 1;
                }
                elsif ( $$plot{Warn} ) {
                    Warn("Warning: $$plot{Warn}\\n");
                }
            }
        }
        if ($fp) {
            close($fp) or $err = 1;
            if ($err) {
                $mt->Unlink($textOut2);
            }
            else {
                $created{$textOut2} = 1;
            }
        }
    }

    my $totWr =
      $countGoodWr +
      $countBadWr +
      $countSameWr +
      $countCopyWr +
      $countGoodCr +
      $countBadCr;

    if ( defined $deleteOrig ) {

        unless ($quiet) {
            printf "%5d directories scanned\\n",    $countDir    if $countDir;
            printf "%5d directories created\\n",    $countNewDir if $countNewDir;
            printf "%5d files failed condition\\n", $countFailed if $countFailed;
            printf "%5d image files found\\n",      $count;
        }
        if (@delFiles) {
            if ( $deleteOrig == 1 ) {
                printf '%5d originals will be deleted!  Are you sure [y/n]? ',
                  scalar(@delFiles);
                my $response = <STDIN>;
                unless ( $response =~ /^(y|yes)\\s*$/i ) {
                    Warn "Originals not deleted.\\n";
                    next;
                }
            }
            $countGoodWr = $mt->Unlink(@delFiles);
            $countBad    = scalar(@delFiles) - $countGoodWr;
        }
        if ($quiet) {
        }
        elsif ( $count and not $countGoodWr and not $countBad ) {
            printf "%5d original files found\\n", $countGoodWr;
        }
        elsif ($deleteOrig) {
            printf "%5d original files deleted\\n", $countGoodWr if $count;
            printf "%5d originals not deleted due to errors\\n", $countBad
              if $countBad;
        }
        else {
            printf "%5d image files restored from original\\n", $countGoodWr
              if $count;
            printf "%5d files not restored due to errors\\n", $countBad
              if $countBad;
        }

    }
    elsif ( ( not $binaryStdout or $verbose ) and not $quiet ) {

        my $tot = $count + $countBad;
        if (   $countDir
            or $totWr
            or $countFailed
            or $tot > 1
            or $textOut
            or %countLink )
        {
            my $o = ( ( $html or $json or $xml or %printFmt or $csv or $plot )
                  and not $textOut ) ? \\*STDERR : $vout;
            printf( $o "%5d directories scanned\\n", $countDir ) if $countDir;
            printf( $o "%5d directories created\\n", $countNewDir )
              if $countNewDir;
            printf( $o "%5d files failed condition\\n", $countFailed )
              if $countFailed;
            printf( $o "%5d image files created\\n", $countGoodCr )
              if $countGoodCr;
            printf( $o "%5d image files updated\\n", $countGoodWr )
              if $totWr - $countGoodCr - $countBadCr - $countCopyWr;
            printf( $o "%5d image files unchanged\\n", $countSameWr )
              if $countSameWr;
            printf( $o "%5d image files %s\\n",
                $countCopyWr, $overwriteOrig ? 'moved' : 'copied' )
              if $countCopyWr;
            printf( $o "%5d files weren't updated due to errors\\n",
                $countBadWr
            ) if $countBadWr;
            printf( $o "%5d files weren't created due to errors\\n",
                $countBadCr
            ) if $countBadCr;
            printf( $o "%5d image files read\\n", $count )
              if ( $tot + $countFailed ) > 1
              or ( $countDir and not $totWr );
            printf( $o "%5d files could not be read\\n", $countBad )
              if $countBad;
            printf( $o "%5d output files created\\n", scalar( keys %created ) )
              if $textOut or $textOut2;
            printf( $o "%5d output files appended\\n", scalar( keys %appended ) )
              if %appended;
            printf( $o "%5d hard links created\\n", $countLink{Hard} || 0 )
              if $countLink{Hard} or $countLink{BadHard};
            printf( $o "%5d hard links could not be created\\n",
                $countLink{BadHard}
            ) if $countLink{BadHard};
            printf( $o "%5d symbolic links created\\n", $countLink{Sym} || 0 )
              if $countLink{Sym} or $countLink{BadSym};
            printf( $o "%5d symbolic links could not be created\\n",
                $countLink{BadSym}
            ) if $countLink{BadSym};
        }
    }

    if ( $countBadWr or $countBadCr or $countBad ) {
        $rtnVal = 1;
    }
    elsif ( $countFailed and not( $count or $totWr ) and not $rtnVal ) {
        $rtnVal = 2;
    }

    Image::ExifTool::Purge(0) if $purge;

    Cleanup();

}

close STAYOPEN if $stayOpen >= 2;

Exit $rtnValApp;

sub GetImageInfo($$) {
    my ( $et, $orig ) = @_;
    my ( @foundTags, @found2, $info, $info2, $et2, $file, $file2, $ind, $g8 );

    if ( defined $windowTitle ) {
        if ( $progressCount >= $progressNext ) {
            my $prog  = $progressMax ? "$progressCount/$progressMax" : '0/0';
            my $title = $windowTitle;
            my ( $num, $denom ) = split '/', $prog;
            my $frac = $num / ( $denom || 1 );
            my $n    = $title =~ s/%(\\d+)b/%b/ ? $1 : 20;
            my $bar  = int( $frac * $n + 0.5 );
            my %lkup = (
                b   => ( 'I' x $bar ) . ( '.' x ( $n - $bar ) ),
                f   => $orig,
                p   => int( 100 * $frac + 0.5 ),
                r   => $prog,
                '%' => '%',
            );
            $title =~ s/%([%bfpr])/$lkup{$1}/eg;
            SetWindowTitle($title);

            if ( defined $progressMax ) {
                undef $progressNext;
            }
            else {
                $progressNext += $progressIncr;
            }
        }
        ++$progressCount unless defined $progressMax;
    }
    unless ( length $orig or $outOpt ) {
        Warn qq(Error: Zero-length file name - ""\\n);
        ++$countBad;
        return;
    }
    if (@srcFmt) {
        my ( $fmt, $first );
        foreach $fmt (@srcFmt) {
            $file = $fmt eq '@' ? $orig : FilenameSPrintf( $fmt, $orig );
            $et->Exists($file) and undef($first), last;
            $verbose and print $vout "Source file $file does not exist\\n";
            $first = $file unless defined $first;
        }
        $file = $first if defined $first;
        my ( $d, $f ) = Image::ExifTool::SplitFileName($orig);
        $et->Options( UserParam => "OriginalDirectory#=$d" );
        $et->Options( UserParam => "OriginalFileName#=$f" );
    }
    else {
        $file = $orig;
    }
    foreach $g8 ( sort keys %altFile ) {
        my $altName = $orig;
        unless ( $altFile{$g8} eq '@' ) {
            $altName =~ s/\\$/\\$\\$/g;
            $altName = FilenameSPrintf( $altFile{$g8}, $altName );
        }
        $et->SetAlternateFile( $g8, $altName );
    }

    my $pipe = $file;
    if ($doUnzip) {
        if ( $file =~ /\\.(gz|bz2)$/i ) {
            my $type = lc $1;
            if ( $file =~ /[^-_.'A-Za-z0-9\\/\\\\]/ ) {
                Warn "Error: Insecure zip file name. Skipped\\n";
                EFile($file);
                ++$countBad;
                return;
            }
            if ( $type eq 'gz' ) {
                $pipe = qq{gzip -dc "$file" |};
            }
            else {
                $pipe = qq{bzip2 -dc "$file" |};
            }
            $$et{TRUST_PIPE} = 1;
        }
    }
    if (@condition) {
        my $result;
        unless ($failCondition) {
            undef $evalWarning;
            local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };

            my ( %info, $condition );
            my $opts = {
                Duplicates  => 1,
                RequestTags => \\@requestTags,
                Verbose     => 0,
                HtmlDump    => 0
            };
            $$opts{FastScan} = $fastCondition if defined $fastCondition;
            @foundTags = ( '*', @tags ) if @tags;
            $info =
              $et->ImageInfo( Infile( $pipe, $isWriting ), \\@foundTags, $opts );
            foreach $condition (@condition) {
                my $cond =
                  $et->InsertTagValues( $condition, \\@foundTags, \\%info );
                {

                    package Image::ExifTool;

                    my $self = $et;
                    $result = eval $cond;

                    $@ and $evalWarning = $@;
                }
                if ($evalWarning) {
                    undef $result;
                    if ($verbose) {
                        chomp $evalWarning;
                        $evalWarning =~ s/ at \\(eval .*//s;
                        Warn "Condition: $evalWarning - $file\\n";
                    }
                }
                last unless $result;
            }
            undef @foundTags if $fastCondition;
        }
        if ($result) {
            undef $info unless $file eq '-' or $et->Exists($file);
        }
        else {
            Progress( $vout, "-------- $file (failed condition)" ) if $verbose;
            EFile( $file, 2 );
            ++$countFailed;
            return;
        }
        if ( $isWriting or $verbose or defined $fastCondition or defined $diff )
        {
            undef $info;
            --$$et{FILE_SEQUENCE};
        }
    }
    elsif ( $file =~ s/^(\\@JSON:)(.*)/$1/ ) {
        my $dat = $2;
        $info = $et->ImageInfo( \\$dat, \\@foundTags );
        if ($geoOnly) {
            /^Geolocation/ or delete $$info{$_} foreach keys %$info;
            $file = ' ';
        }
    }
    if ( defined $deleteOrig ) {
        Progress( $vout, "======== $file" ) if defined $verbose;
        ++$count;
        my $original = "\${file}_original";
        $et->Exists($original) or return;
        if ($deleteOrig) {
            $verbose and print $vout "Scheduled for deletion: $original\\n";
            push @delFiles, $original;
        }
        elsif ( $et->Rename( $original, $file ) ) {
            $verbose and print $vout "Restored from $original\\n";
            EFile( $file, 3 );
            ++$countGoodWr;
        }
        else {
            Warn "Error renaming $original\\n";
            EFile($file);
            ++$countBad;
        }
        return;
    }
    ++$seqFileNum;
    my ($dir) = Image::ExifTool::SplitFileName($orig);
    $seqFileDir = $seqFileDir{$dir} = ( $seqFileDir{$dir} || 0 ) + 1;

    my $lineCount = 0;
    my ( $fp, $outfile, $append );
    if (    $textOut
        and ( $verbose or $et->Options('PrintCSV') )
        and not( $tagOut or defined $diff or $plot ) )
    {
        ( $fp, $outfile, $append ) = OpenOutputFile($orig);
        $fp or EFile($file), ++$countBad, return;
        $tmpText = $outfile unless $append;
        $et->Options( TextOut => $fp );
    }

    if ($isWriting) {
        Progress( $vout, "======== $file" ) if defined $verbose;
        SetImageInfo( $et, $file, $orig );
        $info = $et->GetInfo( 'Warning', 'Error' );
        PrintErrors( $et, $info, $file );
        if ( defined $outfile ) {
            undef $tmpText;
            close($fp);
            $et->Options( TextOut => $vout );
            if ( $info->{Error} ) {
                $et->Unlink($outfile);
            }
            elsif ($append) {
                $appended{$outfile} = 1 unless $created{$outfile};
            }
            else {
                $created{$outfile} = 1;
            }
        }
        return;
    }

    unless ( $file eq '-' or $et->Exists($file) or $info ) {
        Warn "Error: File not found - $file\\n";
        FileNotFound($file);
        defined $outfile and close($fp), undef($tmpText), $et->Unlink($outfile);
        EFile($file);
        ++$countBad;
        return;
    }
    my $o;
    unless ( $binaryOutput
        or $textOut
        or %printFmt
        or $html > 1
        or $csv
        or $plot )
    {
        if ($html) {
            require Image::ExifTool::HTML;
            my $f = Image::ExifTool::HTML::EscapeHTML($file);
            print "<!-- $f -->\\n";
        }
        elsif ( not( $json or $xml or defined $diff ) ) {
            $o = \\*STDOUT if ( $multiFile and not $quiet ) or $progress;
        }
    }
    $o = \\*STDERR                    if $progress and not $o;
    Progress( $o, "======== $file" ) if $o;
    if ($info) {
        if ( @tags and not %printFmt ) {
            @foundTags = @tags;
            $info      = $et->GetInfo( \\@foundTags );
        }
    }
    else {
        my $oldDups = $et->Options('Duplicates');
        if (%printFmt) {
            $et->Options( Duplicates  => 1 );
            $et->Options( RequestTags => \\@requestTags );
            if ( $printFmt{SetTags} ) {
                $$et{TAGS_FROM_FILE} = 1;
                $et->Options( MakerNotes  => 1 );
                $et->Options( Struct      => 2 );
                $et->Options( List        => 1 );
                $et->Options( CoordFormat => '%d %d %.8f' )
                  unless $et->Options('CoordFormat');
            }
        }
        else {
            @foundTags = @tags;
        }
        if ( defined $diff ) {
            $file2 = FilenameSPrintf( $diff, $orig );
            if ( $file eq $file2 ) {
                Warn "Error: Diffing file with itself - $file2\\n";
                EFile($file);
                ++$countBad;
                return;
            }
            if ( $et->Exists($file2) ) {
                $showGroup = 1 unless defined $showGroup;
                $allGroup  = 1 unless defined $allGroup;
                $et->Options(
                    Duplicates => 1,
                    Sort       => "Group$showGroup",
                    Verbose    => 0
                );
                $et2 = Image::ExifTool->new;
                $et2->Options( %{ $$et{OPTIONS} } );
                $et2->Options( ListSep   => $$et{OPTIONS}{ListSep} );
                $et2->Options( ListSplit => $$et{OPTIONS}{ListSplit} );
                @found2 = @foundTags;
                $info2  = $et2->ImageInfo( $file2, \\@found2 );
            }
            else {
                $info2 = { Error => "Diff file not found" };
            }
            if ( $$info2{Error} ) {
                Warn "Error: $$info2{Error} - $file2\\n";
                EFile($file);
                ++$countBad;
                return;
            }
        }
        $info = $et->ImageInfo( Infile($pipe), \\@foundTags );
        $et->Options( Duplicates => $oldDups );
    }

    if ($fp) {
        if ( defined $outfile ) {
            $et->Options( TextOut => \\*STDOUT );
            undef $tmpText;
            if ( $info->{Error} ) {
                close($fp);
                $et->Unlink($outfile);
            }
            else {
                ++$lineCount;
            }
        }
        if ( $info->{Error} ) {
            Warn "Error: $$info{Error} - $file\\n";
            EFile($file);
            ++$countBad;
            return;
        }
    }

    if ( $binaryOutput or not %$info ) {
        my $errs = $et->GetInfo( 'Warning', 'Error' );
        PrintErrors( $et, $errs, $file ) and EFile($file), $rtnVal = 1;
    }
    elsif ( $et->GetValue('Error')
        or ( $$et{Validate} and $et->GetValue('Warning') ) )
    {
        $rtnVal = 1;
    }

    unless ( defined $outfile or $tagOut ) {
        ( $fp, $outfile, $append ) = OpenOutputFile($orig);
        $fp or EFile($file), ++$countBad, return;
        $tmpText = $outfile if defined $outfile and not $append;
    }

    if ( defined $diff ) {
        my ( %done, %done2, $wasDiff, @diffs, @groupTags2 );
        my $v = $verbose || 0;
        print $fp "======== diff < $file > $file2\\n";
        my ( $g2, $same ) = ( 0, 0 );
        for ( ; ; ) {
            my ( $g, $tag2, $i, $key, @dupl, $val2, $t2, $equal, %used );
            my $tag = shift @foundTags;
            if ( defined $tag ) {
                $done{$tag} = 1;
                $g = $et->GetGroup( $tag, $showGroup );
            }
            unless ($g) {
                for ( ; ; ) {
                    $tag2 = shift @found2;
                    defined $tag2 or $g = '', last;
                    $done2{$tag2}
                      or $g = $et2->GetGroup( $tag2, $showGroup ), last;
                }
            }
            if ( $g ne $g2 ) {
                foreach $t2 (@groupTags2) {
                    next if $done2{$t2};
                    my $val2 = $et2->GetValue($t2);
                    next unless defined $val2;
                    my $name =
                        $outFormat < 1
                      ? $et2->GetDescription($t2)
                      : GetTagName($t2);
                    my $len = LengthUTF8($name);
                    my $pad =
                      $outFormat < 2 ? ' ' x ( $len < 32 ? 32 - $len : 0 ) : '';
                    if ($allGroup) {
                        my $grp = "[$g2]";
                        $grp .= ' ' x ( 15 - length($grp) )
                          if length($grp) < 15 and $outFormat < 2;
                        push @diffs, sprintf "> %s %s%s: %s\\n", $grp, $name,
                          $pad, Printable($val2);
                    }
                    else {
                        push @diffs, sprintf "> %s%s: %s\\n", $name, $pad,
                          Printable($val2);
                    }
                    $done2{$t2} = 1;
                }
                my $str = '';
                $v
                  and ( $same or $v > 1 )
                  and $str =
                  "  ($same same tag" . ( $same == 1 ? '' : 's' ) . ')';
                if ( not $allGroup ) {
                    print $fp "---- $g2 ----$str\\n"
                      if $g2 and ( $str or @diffs );
                }
                elsif ( $str and $g2 ) {
                    printf $fp "   %-13s%s\\n", $g2, $str;
                }
                @diffs and print( $fp @diffs ), $wasDiff = 1, @diffs = ();
                last unless $g;
                ( $g2, $same ) = ( $g, 0 );
                @groupTags2 = ();
                push @groupTags2, $tag2 if defined $tag2;
                foreach $t2 (@found2) {
                    $done2{$t2}
                      or $g ne $et2->GetGroup( $t2, $showGroup )
                      or push @groupTags2, $t2;
                }
            }
            next unless defined $tag;
            my $val = $et->GetValue($tag);
            next unless defined $val;
            my $name = GetTagName($tag);
            my $desc = $outFormat < 1 ? $et->GetDescription($tag) : $name;
            my @tags2 = grep /^$name( |$)/, @groupTags2;
          T2: foreach $t2 (@tags2) {
                next if $done2{$t2};
                $tag2 = $t2;
                $val2 = $et2->GetValue($t2);
                next unless defined $val2;
                IsEqual( $val, $val2 ) and $equal = 1, last;
                if ( $$et{DUPL_TAG}{$name} and not @dupl ) {
                    for (
                        $i = 0, $key = $name ;
                        $i <= $$et{DUPL_TAG}{$name} ;
                        ++$i, $key = "$name ($i)"
                      )
                    {
                        push @dupl, $key
                          unless $done{$key}
                          or $g ne $et->GetGroup( $key, $showGroup );
                    }
                    @dupl =
                      sort { $$et{FILE_ORDER}{$a} <=> $$et{FILE_ORDER}{$b} }
                      @dupl
                      if @dupl > 1;
                }
                foreach (@dupl) {
                    next if $used{$_};
                    my $v = $et->GetValue($_);
                    next unless defined($v) and IsEqual( $v, $val2 );
                    $used{$_} = 1;
                    undef($tag2);
                    undef($val2);
                    next T2;
                }
                last;
            }
            if ($equal) {
                ++$same;
            }
            else {
                my $len = LengthUTF8($desc);
                my $pad =
                  $outFormat < 2 ? ' ' x ( $len < 32 ? 32 - $len : 0 ) : '';
                if ($allGroup) {
                    my $grp = "[$g]";
                    $grp .= ' ' x ( 15 - length($grp) )
                      if length($grp) < 15 and $outFormat < 2;
                    push @diffs, sprintf "< %s %s%s: %s\\n", $grp, $desc, $pad,
                      Printable($val);
                    if ( defined $val2 ) {
                        $grp = ' ' x length($grp), $desc = ' ' x $len if $v < 3;
                        push @diffs, sprintf "> %s %s%s: %s\\n", $grp, $desc,
                          $pad, Printable($val2);
                    }
                }
                else {
                    push @diffs, sprintf "< %s%s: %s\\n", $desc, $pad,
                      Printable($val);
                    $desc = ' ' x $len if $v < 3;
                    push @diffs, sprintf "> %s%s: %s\\n", $desc, $pad,
                      Printable($val2)
                      if defined $val2;
                }
            }
            $done2{$tag2} = 1 if defined $tag2;
        }
        print $fp "(no metadata differences)\\n" unless $wasDiff;
        if ( defined $outfile ) {
            $created{$outfile} = 1;
            close($fp);
            undef $tmpText;
        }
        ++$count;
        return;
    }
    $comma = $outComma{$outfile} if $append and ( $textOverwrite & 0x02 );

    if (%printFmt) {
        my ( $type, @doc, $grp, $lastDoc, $cache );
        $fileTrailer = '';
        if ( $et->Options('ExtractEmbedded') ) {
            $lastDoc = $$et{DOC_COUNT} and $cache = {};
        }
        else {
            $lastDoc = 0;
        }
        for ( $doc[0] = 0 ; $doc[0] <= $lastDoc ; ) {
            my $doc = join '-', @doc;
            my ( $skipBody, $opt );
            foreach $type (qw(HEAD SECT IF BODY ENDS TAIL)) {
                my $prf = $printFmt{$type} or next;
                if ( $type eq 'HEAD' and defined $outfile ) {
                    next if $wroteHEAD{$outfile};
                    $wroteHEAD{$outfile} = 1;
                }
                next if $type eq 'BODY' and $skipBody;
                if (
                    $type eq 'IF'
                    or ( ( $doc[0] > 1 or @doc > 1 )
                        and not $$et{OPTIONS}{IgnoreMinorErrors} )
                  )
                {
                    $opt = 'Silent';
                }
                else {
                    $opt = 'Warn';
                }
                if ($lastDoc) {
                    if ($doc) {
                        next if $type eq 'HEAD' or $type eq 'TAIL';
                        $grp = "Doc$doc";
                    }
                    else {
                        $grp = 'Main';
                    }
                }
                my @lines;
                foreach (@$prf) {
                    my $line =
                      $et->InsertTagValues( $_, \\@foundTags, $opt, $grp,
                        $cache );
                    if ( $type eq 'IF' ) {
                        $skipBody = 1 unless defined $line;
                    }
                    elsif ( defined $line ) {
                        push @lines, $line;
                    }
                }
                $lineCount += scalar @lines;
                if ( $type eq 'SECT' ) {
                    my $thisHeader = join '', @lines;
                    if ( $sectHeader and $sectHeader ne $thisHeader ) {
                        print $fp $sectTrailer if $sectTrailer;
                        undef $sectHeader;
                    }
                    $sectTrailer = '';
                    print $fp $sectHeader = $thisHeader unless $sectHeader;
                }
                elsif ( $type eq 'ENDS' ) {
                    $sectTrailer .= join '', @lines if defined $sectHeader;
                }
                elsif ( $type eq 'TAIL' ) {
                    $fileTrailer .= join '', @lines;
                }
                elsif (@lines) {
                    print $fp @lines;
                }
            }
            push @doc, 1;
            while ( @doc > 1 ) {
                my $nextDoc = join '-', @doc;
                last if $$et{HAS_DOC}{$nextDoc};
                pop @doc;
                ++$doc[-1];
            }
        }
        delete $printFmt{HEAD} unless defined $outfile;
        my $errs = $et->GetInfo( 'Warning', 'Error' );
        PrintErrors( $et, $errs, $file ) and EFile($file);
    }
    elsif ($plot) {
        my $tagExtra = $$et{TAG_EXTRA};
        my ( $tag, %docNum );
        foreach $tag ( keys %$info ) {
            next unless $$tagExtra{$tag} and $$tagExtra{$tag}{G3};
            $docNum{$tag} = $1 if $$tagExtra{$tag}{G3} =~ /(\\d+)/;
        }
        $$plot{DocNum} = \\%docNum;
        $$plot{EE}     = 1 if $et->Options('ExtractEmbedded');
        $plot->AddPoints( $info, \\@foundTags );
    }
    elsif ( not $disableOutput ) {
        my ( $tag, $line, %noDups, %csvInfo, $bra, $ket, $sep, $quote );
        if ($fp) {
            if ($fileHeader) {
                print $fp $fileHeader
                  unless defined $outfile
                  and ( $created{$outfile} or $appended{$outfile} );
                undef $fileHeader unless $textOut;
            }
            if ($html) {
                print $fp "<table>\\n";
            }
            elsif ($xml) {
                my $f = $file;
                CleanXML( \\$f );
                print $fp "\\n<rdf:Description rdf:about='\${f}'";
                print $fp "\\n  xmlns:et='http://ns.exiftool.org/1.0/'";
                print $fp
                  " et:toolkit='Image::ExifTool $Image::ExifTool::VERSION'";
                my ( %groups, @groups, $grp0, $grp1 );
                foreach $tag (@foundTags) {
                    ( $grp0, $grp1 ) = $et->GetGroup($tag);
                    unless ($grp1) {
                        next unless defined $forcePrint;
                        $grp0 = $grp1 = 'Unknown';
                    }
                    AddGroups( $$info{$tag}, $grp0, \\%groups, \\@groups )
                      if ref $$info{$tag};
                    next if $groups{$grp1};
                    $groups{$grp1} = $grp0;
                    push @groups, $grp1;
                }
                foreach $grp1 (@groups) {
                    my $grp = $groups{$grp1};
                    unless ($grp eq $grp1
                        and $grp =~ /^(ExifTool|File|Composite|Unknown)$/ )
                    {
                        $grp .= "/$grp1";
                    }
                    print $fp
                      "\\n  xmlns:$grp1='http://ns.exiftool.org/$grp/1.0/'";
                }
                print $fp '>' if $outFormat < 1;
                $ind = $outFormat >= 0 ? ' ' : '   ';
            }
            elsif ($json) {
                ( $bra, $ket, $sep ) =
                  $json == 1 ? ( '{', '}', ':' ) : ( 'Array(', ')', ' =>' );
                $quote = 1
                  if $$et{OPTIONS}{StructFormat}
                  and $$et{OPTIONS}{StructFormat} eq 'JSONQ';
                print $fp ",\\n" if $comma;
                print $fp qq($bra\\n  "SourceFile"$sep ),
                  EscapeJSON( MyConvertFileName( $et, $file ), 1 );
                $comma = 1;
                $ind = ( defined $showGroup and not $allGroup ) ? '    ' : '  ';
            }
            elsif ($csv) {
                my $file2 = MyConvertFileName( $et, $file );
                $database{$file2} = \\%csvInfo;
                push @csvFiles, $file2;
            }
        }
        my $noDups    = ( $json or ( $xml and $outFormat > 0 ) );
        my $printConv = $et->Options('PrintConv');
        my $lastGroup = '';
        my $i         = -1;
      TAG: foreach $tag (@foundTags) {
            ++$i;
            my $tagName = GetTagName($tag);
            my ( $group, $valList );
            my $val = $$info{$tag};
            $isBinary = ( ref $val eq 'SCALAR' and defined $binaryOutput );
            if ( ref $val ) {
                if (    defined $binaryOutput
                    and not $binaryOutput
                    and $$et{TAG_INFO}{$tag}{Protected} )
                {
                    my $lcTag = lc $tag;
                    $lcTag =~ s/ .*//;
                    next
                      unless $$et{REQ_TAG_LOOKUP}{$lcTag}
                      or ( $$et{OPTIONS}{RequestAll} || 0 ) > 2;
                }
                $val = ConvertBinary($val);
                next unless defined $val;
                if ( $structOpt and ref $val ) {
                    $val = Image::ExifTool::XMP::SerializeStruct( $et, $val )
                      unless $xml or $json;
                }
                elsif ( ref $val eq 'ARRAY' ) {
                    if ( defined $listItem ) {
                        $val = $$val[$listItem];
                    }
                    elsif ($binaryOutput) {
                        if ($tagOut) {
                            $valList = $val;
                            $val     = shift @$valList;
                        }
                        else {
                            $val = join defined $binSep ? $binSep : "\\n", @$val;
                        }
                    }
                    elsif ($joinLists) {
                        $val = join $listSep, @$val;
                    }
                }
            }
            if ( not defined $val ) {
                next if $binaryOutput;
                if ( defined $forcePrint ) {
                    $val = $forcePrint;
                }
                elsif ( not $csv ) {
                    next;
                }
            }
            if ( defined $showGroup ) {
                $group = $et->GetGroup( $tag, $showGroup );
                next
                  if $noDups
                  and $tag =~ /^(.*?) ?\\(/
                  and defined $$info{$1}
                  and $group eq $et->GetGroup( $1, $showGroup );
                if ( not $group and ( $xml or $json or $csv ) ) {
                    if ( $showGroup !~ /\\b4\\b/ ) {
                        $group = 'Unknown';
                    }
                    elsif ( $json and not $allGroup ) {
                        $group = 'Copy0';
                    }
                }
                if ( $fp and not( $allGroup or $csv ) ) {
                    if ( $lastGroup ne $group ) {
                        if ($html) {
                            my $cols = 1;
                            ++$cols if $outFormat == 0 or $outFormat == 1;
                            ++$cols if $showTagID;
                            print $fp
"<tr><td colspan=$cols bgcolor='#dddddd'>$group</td></tr>\\n";
                        }
                        elsif ($json) {
                            print $fp "\\n  $ket" if $lastGroup;
                            print $fp ','        if $lastGroup or $comma;
                            print $fp qq(\\n  "$group"$sep $bra);
                            undef $comma;
                            undef %noDups;
                        }
                        else {
                            print $fp "---- $group ----\\n";
                        }
                        $lastGroup = $group;
                    }
                    undef $group;
                }
            }
            elsif ($noDups) {
                next if $tag =~ /^(.*?) ?\\(/ and defined $$info{$1};
            }

            ++$lineCount;

            for ( ; ; ) {
                if ($tagOut) {
                    my $ext = SuggestedExtension( $et, \\$val, $tagName );
                    if ( %wext and ( $wext{$ext} || $wext{'*'} || -1 ) < 0 ) {
                        if ( $verbose and $verbose > 1 ) {
                            print $vout
                              "Not writing $ext output file for $tagName\\n";
                        }
                        next TAG;
                    }
                    my @groups = $et->GetGroup($tag);
                    defined $outfile and close($fp), undef($tmpText);
                    my $org = $et->GetValue('OriginalRawFileName')
                      || $et->GetValue('OriginalFileName');
                    ( $fp, $outfile, $append ) =
                      OpenOutputFile( $orig, $tagName, \\@groups, $ext, $org );
                    $fp or ++$countBad, next TAG;
                    $tmpText = $outfile unless $append;
                }
                if ($binaryOutput) {
                    print $fp $val;
                    print $fp $binTerm if defined $binTerm;
                    if ($tagOut) {
                        if ($append) {
                            $appended{$outfile} = 1 unless $created{$outfile};
                        }
                        else {
                            $created{$outfile} = 1;
                        }
                        close($fp);
                        undef $tmpText;
                        $verbose and print $vout "Wrote $tagName to $outfile\\n";
                        undef $outfile;
                        undef $fp;
                        next TAG unless $valList and @$valList;
                        $val = shift @$valList;
                        next;
                    }
                    next TAG;
                }
                last;
            }
            if ($csv) {
                my $tn = $tagName;
                $tn .= '#' if $tag =~ /#/;
                my $gt = $group ? "$group:$tn" : $tn;
                my $lcTag = lc $gt;
                next if defined $csvInfo{$lcTag} and $tag =~ /\\(/;
                $csvInfo{$lcTag} = $val;
                if ( defined $csvTags{$lcTag} ) {
                    $csvTags{$lcTag} = $gt if defined $$info{$tag};
                    next;
                }
                if (    $group
                    and defined $csvTags[$i]
                    and $csvTags[$i] =~ /^(.*):$tn$/i )
                {
                    next if $group eq 'Unknown';
                    if ( $1 eq 'unknown' ) {
                        delete $csvTags{ $csvTags[$i] };
                        $csvTags{$lcTag} = defined($val) ? $gt : '';
                        $csvTags[$i] = $lcTag;
                        next;
                    }
                }
                $csvTags{$lcTag} = defined($val) ? $gt : '';
                if ( @csvFiles == 1 ) {
                    push @csvTags, $lcTag;
                }
                elsif (@csvTags) {
                    undef @csvTags;
                }
                next;
            }

            my $desc = $outFormat > 0 ? $tagName : $et->GetDescription($tag);

            if ($xml) {
                my $tok = "$group:$tagName";
                if ( $outFormat > 0 ) {
                    if ( $structOpt and ref $val ) {
                        $val =
                          Image::ExifTool::XMP::SerializeStruct( $et, $val );
                    }
                    if ($escapeHTML) {
                        $val =~ tr/\\0-\\x08\\x0b\\x0c\\x0e-\\x1f/./;
                        Image::ExifTool::XMP::FixUTF8( \\$val ) unless $altEnc;
                        $val =
                          Image::ExifTool::HTML::EscapeHTML( $val, $altEnc );
                    }
                    else {
                        CleanXML( \\$val );
                    }
                    unless ( $noDups{$tok} ) {
                        $isCRLF and $val =~ s/\\x0d\\x0a/\\x0a/g;
                        print $fp "\\n $tok='\${val}'";
                        $noDups{$tok} = 1;
                    }
                    next;
                }
                my ( $xtra, $valNum, $descClose );
                if ($showTagID) {
                    my ( $id, $lang ) = $et->GetTagID($tag);
                    if ( $id =~ /^\\d+$/ ) {
                        $id = sprintf( "0x%.4x", $id ) if $showTagID eq 'H';
                    }
                    else {
                        $id = Image::ExifTool::XMP::FullEscapeXML($id);
                    }
                    $xtra = " et:id='\${id}'";
                    $xtra .= " xml:lang='\${lang}'" if $lang;
                }
                else {
                    $xtra = '';
                }
                if ($tabFormat) {
                    my $table = $et->GetTableName($tag);
                    my $index = $et->GetTagIndex($tag);
                    $xtra .= " et:table='\${table}'";
                    $xtra .= " et:index='\${index}'" if defined $index;
                }
                my $lastVal = $val;
                for ( $valNum = 0 ; $valNum < 2 ; ++$valNum ) {
                    $val = FormatXML( $val, $ind, $group );
                    $isCRLF and $val =~ s/\\x0d\\x0a/\\x0a/g;
                    if ( $outFormat >= 0 ) {
                        print $fp "\\n <$tok$xtra$val</$tok>";
                        last;
                    }
                    elsif ( $valNum == 0 ) {
                        CleanXML( \\$desc );
                        if ($xtra) {
                            print $fp "\\n <$tok>";
                            print $fp "\\n  <rdf:Description$xtra>";
                            $descClose = "\\n  </rdf:Description>";
                        }
                        else {
                            print $fp "\\n <$tok rdf:parseType='Resource'>";
                            $descClose = '';
                        }
                        print $fp "\\n   <et:desc>$desc</et:desc>";
                        if ($printConv) {
                            print $fp "\\n   <et:prt$val</et:prt>";
                            $val = $et->GetValue( $tag, 'ValueConv' );
                            $val = '' unless defined $val;
                            next unless IsEqual( $val, $lastVal, 1 );
                            print $fp "$descClose\\n </$tok>";
                            last;
                        }
                    }
                    print $fp "\\n   <et:val$val</et:val>";
                    print $fp "$descClose\\n </$tok>";
                    last;
                }
                next;
            }
            elsif ($json) {
                my $tok = $allGroup ? "$group:$tagName" : $tagName;
                next if $noDups{$tok};
                $noDups{$tok} = 1;
                print $fp ',' if $comma;
                print $fp qq(\\n$ind"$tok"$sep );
                if ( $showTagID or $outFormat < 0 ) {
                    $val = { val => $val };
                    if ($showTagID) {
                        my ( $id, $lang ) = $et->GetTagID($tag);
                        $id = sprintf( '0x%.4x', $id )
                          if $showTagID eq 'H' and $id =~ /^\\d+$/;
                        $$val{lang} = $lang if $lang;
                        $$val{id}   = $id;
                    }
                    if ($tabFormat) {
                        $$val{table} = $et->GetTableName($tag);
                        my $index = $et->GetTagIndex($tag);
                        $$val{index} = $index if defined $index;
                    }
                    if ( $outFormat < 0 ) {
                        $$val{desc} = $desc;
                        if ($printConv) {
                            my $num = $et->GetValue( $tag, 'ValueConv' );
                            $$val{num} = $num
                              if defined $num
                              and not IsEqual( $num, $$val{val}, 1 );
                        }
                        my $ex = $$et{TAG_EXTRA}{$tag};
                        $$val{'fmt'} = $$ex{G6} if defined $$ex{G6};
                        if ( defined $$ex{BinVal} ) {
                            my $max =
                              ( $$et{OPTIONS}{LimitLongValues} - 5 ) / 3;
                            if ( $max >= 0
                                and length( $$ex{BinVal} ) > int($max) )
                            {
                                $max = int $max;
                                $$val{'hex'} = join ' ',
                                  unpack( "(H2)$max", $$ex{BinVal} ), '[...]';
                            }
                            else {
                                $$val{'hex'} = join ' ', unpack '(H2)*',
                                  $$ex{BinVal};
                            }
                        }
                        $$val{rat} = $$ex{Rational}
                          if defined $$ex{Rational} and $$et{OPTIONS}{SaveBin};
                    }
                }
                FormatJSON( $fp, $val, $ind, $quote );
                $comma = 1;
                next;
            }
            my $id;
            if ($showTagID) {
                $id = $et->GetTagID($tag);
                if ( $id =~ /^(\\d+)(\\.\\d+)?$/ ) {
                    $id = sprintf( "0x%.4x", $1 ) if $showTagID eq 'H';
                }
                else {
                    $id = '-';
                }
            }

            if ($escapeC) {
                $val =~
                  s/([\\0-\\x1f\\\\\\x7f])/$escC{$1} || sprintf('\\x%.2x', ord $1)/eg;
            }
            else {
                $val =~ tr/\\x01-\\x1f\\x7f/./;
                $val =~ s/\\x00//g;
                $val =~ s/\\s+$//;
            }

            if ($html) {
                print $fp "<tr>";
                print $fp "<td>$group</td>" if defined $group;
                print $fp "<td>$id</td>"    if $showTagID;
                print $fp "<td>$desc</td>"  if $outFormat <= 1;
                print $fp "<td>$val</td></tr>\\n";
            }
            else {
                my $buff = '';
                if ($tabFormat) {
                    $buff = "$group\\t" if defined $group;
                    $buff .= "$id\\t"   if $showTagID;
                    if ( $outFormat <= 1 ) {
                        $buff .= "$desc\\t$val\\n";
                    }
                    elsif ( defined $line ) {
                        $line .= "\\t$val";
                    }
                    else {
                        $line = $val;
                    }
                }
                elsif ( $outFormat < 0 ) {
                    $buff = "[$group] " if defined $group;
                    $buff .= "$id " if $showTagID;
                    $buff .= "$desc\\n      $val\\n";
                }
                elsif ( $outFormat == 0 or $outFormat == 1 ) {
                    my $wid;
                    my $len = 0;
                    if ( defined $group ) {
                        $buff = sprintf( "%-15s ", "[$group]" );
                        $len  = 16;
                    }
                    if ($showTagID) {
                        $wid = ( $showTagID eq 'D' ) ? 5 : 6;
                        $len += $wid + 1;
                        ( $wid = $len - length($buff) - 1 ) < 1 and $wid = 1;
                        $buff .= sprintf "%\${wid}s ", $id;
                    }
                    $wid = 32 - ( length($buff) - $len );
                    my $padLen = $wid - LengthUTF8($desc);
                    $padLen = 0 if $padLen < 0;
                    $buff .= $desc . ( ' ' x $padLen ) . ": $val\\n";
                }
                elsif ( $outFormat == 2 ) {
                    $buff = "[$group] " if defined $group;
                    $buff .= "$id " if $showTagID;
                    $buff .= "$tagName: $val\\n";
                }
                elsif ($argFormat) {
                    $buff = '-';
                    $buff    .= "$group:" if defined $group;
                    $tagName .= '#'       if $tag =~ /#/;
                    $buff    .= "$tagName=$val\\n";
                }
                else {
                    $buff = "$group " if defined $group;
                    $buff .= "$id " if $showTagID;
                    $buff .= "$val\\n";
                }
                print $fp $buff;
            }
            if ($tagOut) {
                if ($append) {
                    $appended{$outfile} = 1 unless $created{$outfile};
                }
                else {
                    $created{$outfile} = 1;
                }
                close($fp);
                undef $tmpText;
                $verbose and print $vout "Wrote $tagName to $outfile\\n";
                undef $outfile;
                undef $fp;
            }
        }
        if ($fp) {
            if ($html) {
                print $fp "</table>\\n";
            }
            elsif ($xml) {
                print $fp $outFormat < 1 ? "\\n</rdf:Description>\\n" : "/>\\n";
            }
            elsif ($json) {
                print $fp "\\n  $ket" if $lastGroup;
                print $fp "\\n$ket";
                $comma = 1;
            }
            elsif ( $tabFormat and $outFormat > 1 ) {
                print $fp "$line\\n" if defined $line;
            }
        }
    }
    if ( defined $outfile ) {
        if ( $textOverwrite & 0x02 ) {
            $outComma{$outfile}                                    = $comma;
            $outTrailer{$outfile}                                  = '';
            $outTrailer{$outfile} .= $sectTrailer and $sectTrailer = ''
              if $sectTrailer;
            $outTrailer{$outfile} .= $fileTrailer if $fileTrailer;
        }
        else {
            print $fp $sectTrailer and $sectTrailer = '' if $sectTrailer;
            print $fp $fileTrailer                       if $fileTrailer;
        }
        close($fp);
        undef $tmpText;
        if ($lineCount) {
            if ($append) {
                $appended{$outfile} = 1 unless $created{$outfile};
            }
            else {
                $created{$outfile} = 1;
            }
        }
        else {
            $et->Unlink($outfile) unless $append;
        }
        undef $comma;
    }
    ++$count;
}

sub SetImageInfo($$$) {
    my ( $et,      $file,     $orig ) = @_;
    my ( $outfile, $restored, $isTemporary, $isStdout, $outType, $tagsFromSrc );
    my ( $hardLink, $symLink, $testName,    $sameFile );
    my $infile = $file;

    if ( defined $tmpFile ) {
        $et->Unlink($tmpFile);
        undef $tmpFile;
    }
    delete $$et{VALUE}{Error};
    delete $$et{VALUE}{Warning};

    if ( defined $outOpt ) {
        if ( $outOpt =~ /^-(\\.\\w+)?$/ ) {
            $outType  = GetFileType($outOpt) if $1;
            $outfile  = '-';
            $isStdout = 1;
        }
        else {
            $outfile = FilenameSPrintf( $outOpt, $orig );
            if ( $outfile eq '' ) {
                Warn
                  "Error: Can't create file with zero-length name from $orig\\n";
                EFile($infile);
                ++$countBadCr;
                return 0;
            }
        }
        if (
            not $isStdout
            and ( ( $et->IsDirectory($outfile) and not $listDir )
                or $outfile =~ /\\/$/ )
          )
        {
            $outfile .= '/' unless $outfile =~ /\\/$/;
            my $name = $file;
            $name =~ s/^.*\\///s;
            $outfile .= $name;
        }
        else {
            my $srcType = GetFileType($file) || '';
            $outType or $outType = GetFileType($outfile);
            if (    $outType
                and ( $srcType ne $outType or $outType eq 'ICC' )
                and $file ne '-' )
            {
                unless ( CanCreate($outType) ) {
                    my $what = $srcType ? 'other types' : 'scratch';
                    WarnOnce "Error: Can't create $outType files from $what\\n";
                    EFile($infile);
                    ++$countBadCr;
                    return 0;
                }
                if ( $file ne '' ) {
                    $et->RestoreNewValues() unless $restored;
                    $restored = 1;
                    my @setTags = @tags;
                    foreach (@exclude) {
                        push @setTags, "-$_";
                    }
                    my %forceCopy = (
                        ICC => 'ICC_Profile',
                        VRD => 'CanonVRD',
                        DR4 => 'CanonDR4',
                    );
                    push @setTags, $forceCopy{$outType} if $forceCopy{$outType};
                    if ( not %setTags or ( @setTags and not $setTags{'@'} ) ) {
                        return 0 unless DoSetFromFile( $et, $file, \\@setTags );
                    }
                    elsif (@setTags) {
                        push @setTags, @{ $setTags{'@'} };
                        $tagsFromSrc = \\@setTags;
                    }
                    $file = '';
                }
            }
        }
        unless ($isStdout) {
            $outfile = NextUnusedFilename($outfile);
            if ( $et->Exists( $outfile, 1 ) and not $doSetFileName ) {
                Warn "Error: '\${outfile}' already exists - $infile\\n";
                EFile($infile);
                ++$countBadWr;
                return 0;
            }
        }
    }
    elsif ( $file eq '-' ) {
        $isStdout = 1;
    }
    if (@dynamicFiles) {
        $et->RestoreNewValues() unless $restored;
        my ( $dyFile, %setTagsIndex );
        foreach $dyFile (@dynamicFiles) {
            if ( not ref $dyFile ) {
                my ( $fromFile, $setTags );
                if ( $dyFile eq '@' ) {
                    $fromFile = $orig;
                    $setTags  = $tagsFromSrc || $setTags{$dyFile};
                }
                else {
                    $fromFile = FilenameSPrintf( $dyFile, $orig );
                    defined $fromFile
                      or EFile($infile), ++$countBadWr, return 0;
                    $setTags = $setTags{$dyFile};
                }
                if ( $setTagsList{$dyFile} ) {
                    my $i = $setTagsIndex{$dyFile} || 0;
                    $setTagsIndex{$dyFile} = $i + 1;
                    $setTags = $setTagsList{$dyFile}[$i]
                      if $setTagsList{$dyFile}[$i];
                }
                return 0 unless DoSetFromFile( $et, $fromFile, $setTags );
            }
            elsif ( ref $dyFile eq 'ARRAY' ) {
                my $fname = FilenameSPrintf( $$dyFile[1], $orig );
                my ( $buff, $rtn, $wrn );
                my $opts = $$dyFile[2];
                if ( defined $fname and SlurpFile( $fname, \\$buff ) ) {
                    $verbose
                      and print $vout "Reading $$dyFile[0] from $fname\\n";
                    ( $rtn, $wrn ) =
                      $et->SetNewValue( $$dyFile[0], $buff, %$opts );
                    $wrn and Warn "$wrn\\n";
                }
                $rtn
                  or $et->SetNewValue(
                    $$dyFile[0], undef,
                    Replace      => 2,
                    ProtectSaved => $$opts{ProtectSaved}
                  );
                next;
            }
            elsif ( ref $dyFile eq 'SCALAR' ) {
                my ( $f, $found, $csvTag, $tg, $csvEtPrt, $csvEtVal );
                undef $evalWarning;
                local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
                my $old = $et->Options('Charset');
                $et->Options( Charset => 'UTF8' ) if $csv eq 'JSON';
                foreach $f ( '*', MyConvertFileName( $et, $file ) ) {
                    my $csvInfo = $database{$f};
                    unless ($csvInfo) {
                        next if $f eq '*';
                        my $absPath = AbsPath($f);
                        if ( defined $absPath and $database{$absPath} ) {
                            $csvInfo = $database{$absPath};
                        }
                        elsif ( $database{ lc $f } ) {
                            $csvInfo = $database{ lc $f };
                        }
                        elsif ( defined $absPath and $database{ lc $absPath } )
                        {
                            $csvInfo = $database{ lc $absPath };
                        }
                        else {
                            next;
                        }
                    }
                    $found = 1;
                    if ($verbose) {
                        print $vout "Setting new values from $csv database\\n";
                        print $vout 'Including tags: ', join( ' ', @tags ), "\\n"
                          if @tags;
                        print $vout 'Excluding tags: ',
                          join( ' ', @csvExclude ), "\\n"
                          if @csvExclude;
                    }
                    if (@tags) {
                        $csvEtPrt = Image::ExifTool->new unless $csvEtPrt;
                        foreach $csvTag ( OrderedKeys($csvInfo) ) {
                            next
                              if $csvTag =~
/^([-_0-9A-Z]+:)*(SourceFile|Directory|FileName)$/i;
                            my @grps = split /:/, $csvTag;
                            my $name = pop @grps;
                            unshift @grps, 'All' while @grps < 2;
                            if ( $name =~ s/#$// ) {
                                $csvEtVal = Image::ExifTool->new
                                  unless $csvEtVal;
                                $csvEtVal->FoundTag( $name, $$csvInfo{$csvTag},
                                    @grps );
                            }
                            else {
                                $csvEtPrt->FoundTag( $name, $$csvInfo{$csvTag},
                                    @grps );
                            }
                        }
                        next;
                    }
                    my @exclTags = @csvExclude;
                    foreach (@exclTags) {
                        tr/-0-9a-zA-Z_:#?*//dc;
                        s/(^|:)(all:)+/$1/ig;
                        s/(^|:)all(#?)$/$1*$2/i;
                        tr/?/./;
                        s/\\*/.*/g;
                    }
                    foreach $csvTag ( OrderedKeys($csvInfo) ) {
                        next
                          if $csvTag =~
                          /^([-_0-9A-Z]+:)*(SourceFile|Directory|FileName)$/i;
                        if (@exclTags) {
                            my ( $exclTag, $exclGrp, $excluded );
                          ExclMatch: foreach $exclTag (@exclTags) {
                                if ( $exclTag =~ /:/ ) {
                                    next unless $csvTag =~ /:/;
                                    my @csvGrps  = split /:/, $csvTag;
                                    my @exclGrps = split /:/, $exclTag;
                                    my $exclName = pop @exclGrps;
                                    next unless pop(@csvGrps) =~ /^$exclName$/i;
                                    foreach $exclGrp (@exclGrps) {
                                        next ExclMatch
                                          unless grep /^$exclGrp$/i, @csvGrps;
                                    }
                                    $excluded = 1;
                                    last;
                                }
                                $csvTag =~ /^([-_0-9A-Z]+:)*$exclTag$/i
                                  and $excluded = 1, last;
                            }
                            next if $excluded;
                        }
                        my ( $rtn, $wrn ) = $et->SetNewValue(
                            $csvTag, $$csvInfo{$csvTag},
                            Protected    => 1,
                            AddValue     => $dbAdd,
                            ProtectSaved => $dbSaveCount
                        );
                        $wrn and Warn "$wrn\\n" if $verbose;
                    }
                }
                if ($csvEtPrt) {
                    my @excl = map "-$_", @csvExclude;
                    my $opts = { AddValue => $dbAdd, Replace => 0 };
                    $et->SetNewValuesFromFile( $csvEtPrt, $opts, @tags, @excl );
                    if ($csvEtVal) {
                        $$opts{Type} = 'ValueConv';
                        $et->SetNewValuesFromFile( $csvEtVal, $opts, @tags,
                            @excl );
                    }
                }
                $et->Options( Charset => $old ) if $csv eq 'JSON';
                unless ($found) {
                    Warn("No SourceFile '\${file}' in imported $csv database\\n");
                    my $absPath = AbsPath($file);
                    Warn("(full path: '\${absPath}')\\n")
                      if defined $absPath and $absPath ne $file;
                    return 0;
                }
            }
        }
    }
    if ($isStdout) {
        $outfile = \\*STDOUT;
        unless ($binaryStdout) {
            binmode(STDOUT);
            $binaryStdout = 1;
        }
    }
    else {
        $hardLink = $et->GetNewValues('HardLink');
        $symLink  = $et->GetNewValues('SymLink');
        $testName = $et->GetNewValues('TestName');
        $hardLink = FilenameSPrintf( $hardLink, $orig ) if defined $hardLink;
        $symLink  = FilenameSPrintf( $symLink,  $orig ) if defined $symLink;
        my $newFileName = $et->GetNewValues('FileName');
        my $newDir      = $et->GetNewValues('Directory');
        if ( defined $newFileName and not length $newFileName ) {
            Warning( $et, "New file name is empty - $infile" );
            undef $newFileName;
        }
        if ( defined $testName ) {
            my $err;
            $err = "You shouldn't write FileName or Directory with TestFile"
              if defined $newFileName or defined $newDir;
            $err = "The -o option shouldn't be used with TestFile"
              if defined $outfile;
            $err
              and Warn("Error: $err - $infile\\n"), EFile($infile),
              ++$countBadWr, return 0;
            $testName = FilenameSPrintf( $testName, $orig );
            $testName = Image::ExifTool::GetNewFileName( $file, $testName )
              if $file ne '';
        }
        if (   defined $newFileName
            or defined $newDir
            or ( $doSetFileName and defined $outfile ) )
        {
            if ($newFileName) {
                $newFileName = FilenameSPrintf( $newFileName, $orig );
                if ( defined $outfile ) {
                    $outfile =
                      Image::ExifTool::GetNewFileName( $file, $outfile )
                      if $file ne '';
                    $outfile =
                      Image::ExifTool::GetNewFileName( $outfile, $newFileName );
                }
                elsif ( $file ne '' ) {
                    $outfile =
                      Image::ExifTool::GetNewFileName( $file, $newFileName );
                }
            }
            if ($newDir) {
                $newDir  = FilenameSPrintf( $newDir, $orig );
                $outfile = Image::ExifTool::GetNewFileName(
                    defined $outfile ? $outfile : $file, $newDir );
            }
            $outfile = NextUnusedFilename( $outfile, $infile );
            if ( $et->Exists( $outfile, 1 ) ) {
                if ( $infile eq $outfile ) {
                    undef $outfile;

                }
                elsif ( $et->IsSameFile( $infile, $outfile ) ) {
                    $sameFile = $outfile;
                }
                else {
                    Warn "Error: '\${outfile}' already exists - $infile\\n";
                    EFile($infile);
                    ++$countBadWr;
                    return 0;
                }
            }
        }
        if ( defined $outfile ) {
            defined $verbose and print $vout "'\${infile}' --> '\${outfile}'\\n";
            CreateDirectory($outfile);
            $tmpFile = $outfile if defined $outOpt;
        }
        unless ( defined $tmpFile ) {
            my ( $numSet, $numPseudo ) = $et->CountNewValues();
            if ( $numSet != $numPseudo and $et->IsDirectory($file) ) {
                print $vout "Can't write real tags to a directory - $infile\\n"
                  if defined $verbose;
                $numSet = $numPseudo;
            }
            if ( $et->Exists($file) ) {
                unless ($numSet) {
                    print $vout "Nothing changed in $file\\n"
                      if defined $verbose;
                    EFile( $infile, 1 );
                    ++$countSameWr;
                    return 1;
                }
            }
            elsif ( CanCreate($file) ) {
                if ( $numSet == $numPseudo ) {
                    Warn("Error: Nothing to write - $file\\n");
                    EFile( $infile, 1 );
                    ++$countBadWr;
                    return 0;
                }
                unless ( defined $outfile ) {
                    $outfile = $file;
                    $file    = '';
                }
            }
            else {
                Warn "Error: File not found - $file\\n";
                EFile($infile);
                FileNotFound($file);
                ++$countBadWr;
                return 0;
            }
            if ( $numSet == $numPseudo ) {
                my ( $r0, $r1, $r2, $r3 ) = ( 0, 0, 0, 0 );
                if ( defined $outfile ) {
                    $r0   = $et->SetFileName( $file, $outfile );
                    $file = $$et{NewName} if $r0 > 0;
                }
                unless ( $r0 < 0 ) {
                    $r1 =
                      $et->SetFileModifyDate( $file, undef, 'FileCreateDate' );
                    $r2 = $et->SetFileModifyDate($file);
                    $r3 = $et->SetSystemTags($file);
                }
                if ( $r0 > 0 or $r1 > 0 or $r2 > 0 or $r3 > 0 ) {
                    EFile( $infile, 3 );
                    ++$countGoodWr;
                }
                elsif ( $r0 < 0 or $r1 < 0 or $r2 < 0 or $r3 < 0 ) {
                    EFile($infile);
                    ++$countBadWr;
                    return 0;
                }
                else {
                    EFile( $infile, 1 );
                    ++$countSameWr;
                }
                if (   defined $hardLink
                    or defined $symLink
                    or defined $testName )
                {
                    DoHardLink( $et, $file, $hardLink, $symLink, $testName );
                }
                return 1;
            }
            if ( not defined $outfile or defined $sameFile ) {
                $outfile = "\${file}_exiftool_tmp";
                if ( $et->Exists($outfile) ) {
                    Warn("Error: Temporary file already exists: $outfile\\n");
                    EFile($infile);
                    ++$countBadWr;
                    return 0;
                }
                $isTemporary = 1;
            }
            $tmpFile = $outfile;
        }
    }
    my $success = $et->WriteInfo( Infile($file), $outfile, $outType );

    if ( $success
        and ( defined $hardLink or defined $symLink or defined $testName ) )
    {
        my $src = defined $outfile ? $outfile : $file;
        DoHardLink( $et, $src, $hardLink, $symLink, $testName );
    }

    my ( $aTime, $mTime, $cTime, $doPreserve );
    $doPreserve = $preserveTime unless $file eq '';
    if ( $doPreserve and $success ) {
        ( $aTime, $mTime, $cTime ) = $et->GetFileTime($file);
        undef $cTime if $$et{WRITTEN}{FileCreateDate};
        if ( $$et{WRITTEN}{FileModifyDate} or $doPreserve == 2 ) {
            if ( defined $cTime ) {
                undef $aTime;
                undef $mTime;
            }
            else {
                undef $doPreserve;
            }
        }
    }

    if ( $success == 1 ) {
        if ( defined $tmpFile ) {
            if ( $et->Exists($file) ) {
                $et->SetFileTime( $tmpFile, $aTime, $mTime, $cTime )
                  if $doPreserve;
                if ($isTemporary) {
                    $et->CopyFileAttrs( $file, $outfile );
                    my $original = "\${file}_original";
                    if ( not $overwriteOrig and not $et->Exists($original) ) {
                        if ( not $et->Rename( $file, $original )
                            or $et->Exists($file) )
                        {
                            Error "Error renaming $file\\n";
                            return 0;
                        }
                    }
                    my $dstFile = defined $sameFile ? $sameFile : $file;
                    if ( $overwriteOrig > 1 ) {
                        my ( $err, $buff );
                        my $newFile = $tmpFile;
                        $et->Open( \\*NEW_FILE, $newFile )
                          or Error("Error opening $newFile\\n"), return 0;
                        binmode(NEW_FILE);

                        $critical = 1;
                        undef $tmpFile;
                        if ( $et->Open( \\*ORIG_FILE, $file, '+<' ) ) {
                            binmode(ORIG_FILE);
                            while ( read( NEW_FILE, $buff, 65536 ) ) {
                                print ORIG_FILE $buff or $err = 1;
                            }
                            close(NEW_FILE);
                            eval { truncate( ORIG_FILE, tell(ORIG_FILE) ) }
                              or $err = 1;
                            close(ORIG_FILE) or $err = 1;
                            if ($err) {
                                Warn "Couldn't overwrite in place - $file\\n";
                                unless (
                                    $et->Rename( $newFile, $file )
                                    or (    $et->Unlink($file)
                                        and $et->Rename( $newFile, $file ) )
                                  )
                                {
                                    Error("Error renaming $newFile to $file\\n");
                                    undef $critical;
                                    SigInt() if $interrupted;
                                    return 0;
                                }
                            }
                            else {
                                $et->SetFileModifyDate( $file, $cTime,
                                    'FileCreateDate', 1 );
                                $et->SetFileModifyDate( $file, $mTime,
                                    'FileModifyDate', 1 );
                                $et->Unlink($newFile);
                                if ($doPreserve) {
                                    $et->SetFileTime( $file, $aTime, $mTime,
                                        $cTime );
                                    $preserveTime{$file} =
                                      [ $aTime, $mTime, $cTime ];
                                }
                            }
                            EFile( $infile, 3 );
                            ++$countGoodWr;
                        }
                        else {
                            close(NEW_FILE);
                            Warn "Error opening $file for writing\\n";
                            EFile($infile);
                            $et->Unlink($newFile);
                            ++$countBadWr;
                        }
                        undef $critical;
                        SigInt() if $interrupted;

                    }
                    elsif ( $et->Rename( $tmpFile, $dstFile ) ) {
                        EFile( $infile, 3 );
                        ++$countGoodWr;
                    }
                    else {
                        my $newFile = $tmpFile;
                        undef $tmpFile;

                        if ( not $et->Unlink($file) ) {
                            Warn "Error renaming temporary file to $dstFile\\n";
                            EFile($infile);
                            $et->Unlink($newFile);
                            ++$countBadWr;
                        }
                        elsif ( not $et->Rename( $newFile, $dstFile ) ) {
                            Warn "Error renaming temporary file to $dstFile\\n";
                            EFile($infile);
                            ++$countBadWr;
                        }
                        else {
                            EFile( $infile, 3 );
                            ++$countGoodWr;
                        }
                    }
                }
                elsif ($overwriteOrig) {
                    EFile( $infile, 3 );
                    $et->Unlink($file) or Warn "Error erasing original $file\\n";
                    ++$countGoodWr;
                }
                else {
                    EFile( $infile, 4 );
                    ++$countGoodCr;
                }
            }
            else {
                EFile( $infile, 4 );
                ++$countGoodCr;
            }
        }
        else {
            EFile( $infile, 3 );
            ++$countGoodWr;
        }
    }
    elsif ($success) {
        EFile( $infile, 1 );
        if ($isTemporary) {
            $et->Unlink($tmpFile);
            ++$countSameWr;
        }
        else {
            $et->SetFileTime( $outfile, $aTime, $mTime, $cTime ) if $doPreserve;
            if ($overwriteOrig) {
                $et->Unlink($file) or Warn "Error erasing original $file\\n";
            }
            ++$countCopyWr;
        }
        print $vout "Nothing changed in $file\\n" if defined $verbose;
    }
    else {
        EFile($infile);
        $et->Unlink($tmpFile) if defined $tmpFile;
        ++$countBadWr;
    }
    undef $tmpFile;
    return $success;
}

sub DoHardLink($$$$$) {
    my ( $et, $src, $hardLink, $symLink, $testName ) = @_;
    if ( defined $hardLink ) {
        $hardLink = NextUnusedFilename($hardLink);
        if ( $et->SetFileName( $src, $hardLink, 'Link' ) > 0 ) {
            $countLink{Hard} = ( $countLink{Hard} || 0 ) + 1;
        }
        else {
            $countLink{BadHard} = ( $countLink{BadHard} || 0 ) + 1;
        }
    }
    if ( defined $symLink ) {
        $symLink = NextUnusedFilename($symLink);
        if ( $et->SetFileName( $src, $symLink, 'SymLink' ) > 0 ) {
            $countLink{Sym} = ( $countLink{Sym} || 0 ) + 1;
        }
        else {
            $countLink{BadSym} = ( $countLink{BadSym} || 0 ) + 1;
        }
    }
    if ( defined $testName ) {
        $testName = NextUnusedFilename( $testName, $src );
        if ( $usedFileName{$testName} ) {
            $et->Warn("File '\${testName}' would exist");
        }
        elsif (
            $et->SetFileName( $src, $testName, 'Test',
                $usedFileName{$testName} ) == 1
          )
        {
            $usedFileName{$testName} = 1;
            $usedFileName{$src}      = 0;
        }
    }
}

sub CleanXML($) {
    my $strPt = shift;
    $$strPt =~ tr/\\0-\\x08\\x0b\\x0c\\x0e-\\x1f/./;
    Image::ExifTool::XMP::FixUTF8($strPt) unless $altEnc;
    $$strPt = Image::ExifTool::XMP::EscapeXML($$strPt);
}

sub EncodeXML($) {
    my $strPt = shift;
    if ( $$strPt =~ /[\\0-\\x08\\x0b\\x0c\\x0e-\\x1f]/
        or ( not $altEnc and Image::ExifTool::IsUTF8($strPt) < 0 ) )
    {
        $$strPt = Image::ExifTool::XMP::EncodeBase64($$strPt);
        return 'http://www.w3.org/2001/XMLSchema#base64Binary';
    }
    elsif ($escapeHTML) {
        $$strPt = Image::ExifTool::HTML::EscapeHTML( $$strPt, $altEnc );
    }
    else {
        $$strPt = Image::ExifTool::XMP::EscapeXML($$strPt);
    }
    return '';
}

sub FormatXML($$$) {
    local $_;
    my ( $val, $ind, $grp ) = @_;
    my $gt = '>';
    if ( ref $val eq 'ARRAY' ) {
        my $val2 = "\\n$ind <rdf:Bag>";
        foreach (@$val) {
            $val2 .=
              "\\n$ind  <rdf:li" . FormatXML( $_, "$ind  ", $grp ) . "</rdf:li>";
        }
        $val = "$val2\\n$ind </rdf:Bag>\\n$ind";
    }
    elsif ( ref $val eq 'HASH' ) {
        $gt = " rdf:parseType='Resource'>";
        my $val2 = '';
        foreach ( OrderedKeys($val) ) {
            my ( $ns, $tg ) = ( $grp, $_ );
            if (/^(.*?):(.*)/) {
                if ( $grp eq 'JSON' ) {
                    $tg =~ tr/:/_/;
                }
                else {
                    ( $ns, $tg ) = ( $1, $2 );
                }
            }
            my $name;
            foreach $name ( $ns, $tg ) {
                $name =~ tr/-_A-Za-z0-9.//dc;
                $name = '_' . $name if $name !~ /^[_A-Za-z]/;
            }
            my $tok = $ns . ':' . $tg;
            $val2 .=
                "\\n$ind <$tok"
              . FormatXML( $$val{$_}, "$ind ", $grp )
              . "</$tok>";
        }
        $val = "$val2\\n$ind";
    }
    else {
        my $enc = EncodeXML( \\$val );
        $gt = " rdf:datatype='\${enc}'>\\n" if $enc;
    }
    return $gt . $val;
}

sub EscapeJSON($;$) {
    my ( $str, $quote ) = @_;
    unless ($quote) {
        return lc($str) if $str =~ /^(true|false)$/i and $json < 2;
        return $str
          if $str =~ /^-?(\\d|[1-9]\\d{1,14})(\\.\\d{1,16})?(e[-+]?\\d{1,3})?$/i;
    }
    if (    $json < 2
        and defined $binaryOutput
        and Image::ExifTool::IsUTF8( \\$str ) < 0 )
    {
        return '"base64:' . Image::ExifTool::XMP::EncodeBase64( $str, 1 ) . '"';
    }
    $str =~ s/(["\\t\\n\\r\\\\])/\\\\$jsonChar{$1}/sg;
    if ( $json < 2 ) {
        $str =~ tr/\\0//d;

        $str =~ s/([\\0-\\x1f\\x7f])/sprintf("\\\\u%.4X",ord $1)/sge;
        Image::ExifTool::XMP::FixUTF8( \\$str ) unless $altEnc;
    }
    else {
        $str =~ s/\\0+$// unless $isBinary;

        $str =~ s/\\$/\\\\\\$/sg;
        $str =~ s/([\\0-\\x1f\\x7f])/sprintf("\\\\x%.2X",ord $1)/sge;
    }
    return '"' . $str . '"';
}

sub FormatJSON($$$;$) {
    local $_;
    my ( $fp, $val, $ind, $quote ) = @_;
    my $comma;
    if ( not ref $val ) {
        print $fp EscapeJSON( $val, $quote );
    }
    elsif ( ref $val eq 'ARRAY' ) {
        if ( $joinLists and not ref $$val[0] ) {
            print $fp EscapeJSON( join( $listSep, @$val ), $quote );
        }
        else {
            my ( $bra, $ket ) = $json == 1 ? ( '[', ']' ) : ( 'Array(', ')' );
            print $fp $bra;
            foreach (@$val) {
                print $fp ',' if $comma;
                FormatJSON( $fp, $_, $ind, $quote );
                $comma = 1,;
            }
            print $fp $ket,;
        }
    }
    elsif ( ref $val eq 'HASH' ) {
        my ( $bra, $ket, $sep ) =
          $json == 1 ? ( '{', '}', ':' ) : ( 'Array(', ')', ' =>' );
        print $fp $bra;
        foreach ( OrderedKeys($val) ) {
            print $fp ',' if $comma;
            my $key = EscapeJSON( $_, 1 );
            print $fp qq(\\n$ind  $key$sep );
            if (    $showTagID
                and $_ eq 'id'
                and $showTagID eq 'H'
                and $$val{$_} =~ /^\\d+\\.\\d+$/ )
            {
                print $fp qq{"$$val{$_}"};
            }
            else {
                FormatJSON( $fp, $$val{$_}, "$ind  ", $quote );
            }
            $comma = 1,;
        }
        print $fp "\\n$ind$ket",;
    }
    else {
        print $fp '"<err>"';
    }
}

sub FormatCSV($) {
    my $val = shift;
    if (
        $setCharset
        and (
            $val =~ /[^\\x09\\x0a\\x0d\\x20-\\x7e\\x80-\\xff]/
            or
            ( $setCharset eq 'UTF8' and Image::ExifTool::IsUTF8( \\$val ) < 0 )
        )
      )
    {
        $val = 'base64:' . Image::ExifTool::XMP::EncodeBase64( $val, 1 );
    }
    $val = qq{"$val"}
      if $val =~ s/"/""/g
      or $val =~ /(^\\s+|\\s+$)/
      or $val =~ /[\\n\\r]|\\Q$csvDelim/;
    return $val;
}

sub PrintCSV(;$) {
    my $fp = shift || \\*STDOUT;
    my ( $file, $lcTag, @tags );

    @csvTags or @csvTags = sort keys %csvTags;
    foreach $lcTag (@csvTags) {
        push @tags, FormatCSV( $csvTags{$lcTag} ) if $csvTags{$lcTag};
    }
    print $fp join( $csvDelim, 'SourceFile', @tags ), "\\n";
    my $empty = defined($forcePrint) ? $forcePrint : '';
    foreach $file (@csvFiles) {
        my @vals    = ( FormatCSV($file) );
        my $csvInfo = $database{$file};
        foreach $lcTag (@csvTags) {
            next unless $csvTags{$lcTag};
            my $val = $$csvInfo{$lcTag};
            defined $val or push( @vals, $empty ), next;
            push @vals, FormatCSV($val);
        }
        print $fp join( $csvDelim, @vals ), "\\n";
    }
}

sub AddGroups($$$$) {
    my ( $val, $grp, $groupHash, $groupList ) = @_;
    my ( $key, $val2 );
    if ( ref $val eq 'HASH' ) {
        foreach $key ( sort keys %$val ) {
            if ( $key =~ /^(.*?):/ and not $$groupHash{$1} and $grp ne 'JSON' )
            {
                $$groupHash{$1} = $grp;
                push @$groupList, $1;
            }
            AddGroups( $$val{$key}, $grp, $groupHash, $groupList )
              if ref $$val{$key};
        }
    }
    elsif ( ref $val eq 'ARRAY' ) {
        foreach $val2 (@$val) {
            AddGroups( $val2, $grp, $groupHash, $groupList ) if ref $val2;
        }
    }
}

sub ConvertBinary($) {
    my $obj = shift;
    my ( $key, $val );
    if ( ref $obj eq 'HASH' ) {
        foreach $key ( keys %$obj ) {
            next unless ref $$obj{$key};
            $$obj{$key} = ConvertBinary( $$obj{$key} );
            return undef unless defined $$obj{$key};
        }
    }
    elsif ( ref $obj eq 'ARRAY' ) {
        foreach $val (@$obj) {
            next unless ref $val;
            $val = ConvertBinary($val);
            return undef unless defined $val;
        }
    }
    elsif ( ref $obj eq 'SCALAR' ) {
        return undef if $noBinary;
        if ( defined $binaryOutput ) {
            $obj = $$obj;
            if (
                $json == 1
                and ( $obj =~ /[^\\x09\\x0a\\x0d\\x20-\\x7e\\x80-\\xf7]/
                    or Image::ExifTool::IsUTF8( \\$obj ) < 0 )
              )
            {
                $obj =
                  'base64:' . Image::ExifTool::XMP::EncodeBase64( $obj, 1 );
            }
        }
        else {
            my $bOpt = $html ? '' : ', use -b option to extract';
            if ( $$obj =~ /^Binary data \\d+ bytes$/ ) {
                $obj = "($$obj$bOpt)";
            }
            else {
                $obj = '(Binary data ' . length($$obj) . " bytes$bOpt)";
            }
        }
    }
    return $obj;
}

sub IsEqual($$;$) {
    my ( $a, $b, $trueScalar ) = @_;
    return 1 if $a eq $b;
    if ( ref $a eq 'SCALAR' ) {
        return 1 if $trueScalar;
        return 1 if ref $b eq 'SCALAR' and $$a eq $$b;
        return 0;
    }
    if ( ref $a eq 'HASH' and ref $b eq 'HASH' ) {
        return 0 if scalar( keys %$a ) != scalar( keys %$b );
        my $key;
        foreach $key ( keys %$a ) {
            return 0 unless IsEqual( $$a{$key}, $$b{$key}, $trueScalar );
        }
    }
    else {
        return 0 if ref $a ne 'ARRAY' or ref $b ne 'ARRAY' or @$a != @$b;
        my $i;
        for ( $i = 0 ; $i < scalar(@$a) ; ++$i ) {
            return 0 unless IsEqual( $$a[$i], $$b[$i], $trueScalar );
        }
    }
    return 1;
}

sub Printable($) {
    my $val = shift;
    if ( ref $val ) {
        if ($structOpt) {
            require Image::ExifTool::XMP;
            $val = Image::ExifTool::XMP::SerializeStruct( $mt, $val );
        }
        elsif ( ref $val eq 'ARRAY' ) {
            $val = join( $listSep, @$val );
        }
        elsif ( ref $val eq 'SCALAR' ) {
            $val = '(Binary data ' . length($$val) . ' bytes)';
        }
    }
    if ($escapeC) {
        $val =~ s/([\\0-\\x1f\\\\\\x7f])/$escC{$1} || sprintf('\\x%.2x', ord $1)/eg;
    }
    else {
        $val =~ tr/\\x01-\\x1f\\x7f/./;
        $val =~ s/\\x00//g;
        $val =~ s/\\s+$//;
    }
    return $val;
}

sub LengthUTF8($) {
    my $str = shift;
    return length $str unless $fixLen;
    local $SIG{'__WARN__'} = sub { };
    if ( not $$mt{OPTIONS}{EncodeHangs} and eval { require Encode } ) {
        $str = Encode::decode_utf8($str);
    }
    else {
        $str = pack( 'U0C*', unpack 'C*', $str );
    }
    my $len;
    if ( $fixLen == 1 ) {
        $len = length $str;
    }
    else {
        my $gcstr = eval { Unicode::GCString->new($str) };
        if ($gcstr) {
            $len = $gcstr->columns;
        }
        else {
            $len = length $str;
            delete $SIG{'__WARN__'};
            Warning( $mt,
                'Unicode::GCString problem.  Columns may be misaligned' );
            $fixLen = 1;
        }
    }
    return $len;
}

sub AddSetTagsFile($;$) {
    my ( $setFile, $opts ) = @_;
    if ( $setTags{$setFile} ) {
        $setTagsList{$setFile} or $setTagsList{$setFile} = [];
        push @{ $setTagsList{$setFile} }, $setTags{$setFile};
    }
    $setTags{$setFile} = [];

    push @newValues, { SaveCount => ++$saveCount }, "TagsFromFile=$setFile";
    $opts or $opts = {};
    $$opts{ProtectSaved} = $saveCount;
    push @{ $setTags{$setFile} }, $opts;
}

sub Infile($;$) {
    my ( $file, $bufferStdin ) = @_;
    if ( $file eq '-' and ( $bufferStdin or $rafStdin ) ) {
        if ($rafStdin) {
            $rafStdin->Seek(0);
        }
        elsif ( open RAF_STDIN, '-' ) {
            $rafStdin = File::RandomAccess->new( \\*RAF_STDIN );
            $rafStdin->BinMode();
        }
        return $rafStdin if $rafStdin;
    }
    return $file;
}

sub Warning($$) {
    my ( $et, $str ) = @_;
    my $noWarn = $et->Options('NoWarning');
    if ( not defined $noWarn or not eval { $str =~ /$noWarn/ } ) {
        Warn "Warning: $str\\n";
    }
}

sub DoSetFromFile($$$) {
    local $_;
    my ( $et, $file, $setTags ) = @_;
    $verbose and print $vout "Setting new values from $file\\n";
    my $info   = $et->SetNewValuesFromFile( Infile( $file, 1 ), @$setTags );
    my $numSet = scalar( keys %$info );
    if ( $$info{Error} ) {
        my @warns = grep /^(Error|Warning)\\b/, keys %$info;
        $numSet -= scalar(@warns);
        my $err = $$info{Error};
        delete $$info{$_} foreach @warns;
        my $noWarn = $et->Options('NoWarning');
        $$info{Warning} = $err
          unless defined $noWarn and eval { $err =~ /$noWarn/ };
    }
    elsif ( $$info{Warning} ) {
        my $warns = 1;
        ++$warns while $$info{"Warning ($warns)"};
        $numSet -= $warns;
    }
    PrintErrors( $et, $info, $file ) and EFile($file), ++$countBadWr, return 0;
    Warning( $et, "No writable tags set from $file" ) unless $numSet;
    return 1;
}

sub CleanFilename($) {
    $_[0] =~ tr/\\\\/\\// if Image::ExifTool::IsPC();
}

sub HasWildcards($) {
    my $path = shift;

    return 0 if $^O eq 'MSWin32' and $path =~ m{^[\\\\/]{2}\\?[\\\\/]};
    return $path =~ /[*?]/;
}

sub CheckUTF8($$) {
    my ( $file, $enc ) = @_;
    my $isUTF8 = 0;
    if ( $file =~ /[\\x80-\\xff]/ ) {
        $isUTF8 = Image::ExifTool::IsUTF8( \\$file );
        if ( $isUTF8 < 0 ) {
            if ($enc) {
                Warn("Invalid filename encoding for $file\\n");
            }
            elsif ( not defined $enc ) {
                WarnOnce(
qq{FileName encoding not specified.  Use "-charset FileName=CHARSET"\\n}
                );
            }
        }
    }
    return $isUTF8;
}

sub SetWindowTitle($) {
    my $title = shift;
    if ( $curTitle ne $title ) {
        $curTitle = $title;
        if ( $^O eq 'MSWin32' ) {
            $title =~ tr(-_a-zA-Z0-9 \\(\\)[]{}%.+/:;,=?*!@#$~')()dc;
            eval { system qq{title $title} };
        }
        else {
            printf STDERR "\\033]0;%s\\007", $title;
        }
    }
}

sub ProcessFiles($;$) {
    my ( $et, $list ) = @_;
    my $enc = $et->Options('CharsetFileName');
    my $file;
    foreach $file (@files) {
        $et->Options( CharsetFileName => 'UTF8' ) if $utf8FileName{$file};
        if ( defined $progressMax ) {
            unless ( defined $progressNext ) {
                $progressNext = $progressCount + $progressIncr;
                $progressNext -= $progressNext % $progressIncr;
                $progressNext = $progressMax if $progressNext > $progressMax;
            }
            ++$progressCount;
            if ($progress) {
                if ( $progressCount >= $progressNext ) {
                    $progStr = " [$progressCount/$progressMax]";
                }
                else {
                    undef $progStr;
                }
            }
        }
        if ( $et->IsDirectory($file) and not $listDir ) {
            $multiFile = $validFile = 1;
            ScanDir( $et, $file, $list );
        }
        elsif ( $filterFlag and not AcceptFile($file) ) {
            if ( $et->Exists($file) ) {
                $filtered = 1;
                Progress( $vout, "-------- $file (wrong extension)" )
                  if $verbose;
            }
            else {
                Error "Error: File not found - $file\\n";
                FileNotFound($file);
            }
        }
        else {
            $validFile = 1;
            if ($list) {
                push( @$list, $file );
            }
            else {
                if (%endDir) {
                    my ( $d, $f ) = Image::ExifTool::SplitFileName($file);
                    next if $endDir{$d};
                }
                GetImageInfo( $et, $file );
                Image::ExifTool::Purge($purge) if $purge;
                $end and Warn("End called - $file\\n");
                if ($endDir) {
                    Warn("EndDir called - $file\\n");
                    my ( $d, $f ) = Image::ExifTool::SplitFileName($file);
                    $endDir{$d} = 1;
                    undef $endDir;
                }
            }
        }
        $et->Options( CharsetFileName => $enc ) if $utf8FileName{$file};
        last                                    if $end;
    }
}

sub ScanDir($$;$) {
    local $_;
    my ( $et, $dir, $list ) = @_;
    my ( @fileList, $done, $file, $utf8Name, $winSurrogate, $endThisDir );
    my $enc = $et->Options('CharsetFileName');
    if ($enc) {
        unless ( $enc eq 'UTF8' ) {
            $dir = $et->Decode( $dir, $enc, undef, 'UTF8' );
            $et->Options( CharsetFileName => 'UTF8' );
        }
        $utf8Name = 1;
    }
    return if $ignore{$dir};
    if ( $^O eq 'MSWin32' and not HasWildcards($dir) ) {
        undef $evalWarning;
        local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
        if ( CheckUTF8( $dir, $enc ) >= 0 ) {
            if ( eval { require Win32::FindFile } ) {
                eval {
                    @fileList = Win32::FindFile::ReadDir($dir);
                    $_        = $_->cFileName foreach @fileList;
                };
                $@ and $evalWarning = $@;
                if ($evalWarning) {
                    chomp $evalWarning;
                    $evalWarning =~ s/ at .*//s;
                    Warning( $et, "[Win32::FindFile] $evalWarning - $dir" );
                    $winSurrogate = 1 if $evalWarning =~ /surrogate/;
                }
                else {
                    $et->Options( CharsetFileName => 'UTF8' );
                    $utf8Name = 1;
                    $done     = 1;
                }
            }
            else {
                $done = 0;
            }
        }
    }
    unless ($done) {
        unless ( opendir( DIR_HANDLE, $dir ) ) {
            Warn("Error opening directory $dir\\n");
            return;
        }
        @fileList = readdir(DIR_HANDLE);
        closedir(DIR_HANDLE);
        if ( defined $done ) {
            foreach $file ( $dir, @fileList ) {
                next unless $file =~ /[\\?\\x80-\\xff]/;
                WarnOnce(
"Install Win32::FindFile to support Windows Unicode file names in directories\\n"
                );
                last;
            }
        }
    }
    $dir =~ /\\/$/ or $dir .= '/';
    foreach $file (@fileList) {
        next if $file eq '.' or $file eq '..';
        my $path = "$dir$file";
        if ( $et->IsDirectory($path) ) {
            next unless $recurse;
            next if $file =~ /^\\./ and $recurse == 1;
            next if $ignore{$file} or ( $ignore{SYMLINKS} and -l $path );
            ScanDir( $et, $path, $list );
            last if $end;
            next;
        }
        next if $endThisDir;
        next if $ignoreHidden and $file =~ /^\\./;

        my $accepted;
        if ($filterFlag) {
            $accepted = AcceptFile($file) or next;
            $accepted &= 0x01;
        }
        unless ($accepted) {
            if ($scanWritable) {
                if ( $scanWritable eq '1' ) {
                    next unless CanWrite($file);
                }
                else {
                    my $type = GetFileType($file);
                    next unless defined $type and $type eq $scanWritable;
                }
            }
            elsif ( not GetFileType($file) ) {
                next unless $doUnzip;
                next unless $file =~ /\\.(gz|bz2)$/i;
            }
        }
        if (    $winSurrogate
            and $isWriting
            and ( not $overwriteOrig or $overwriteOrig != 2 )
            and not $doSetFileName
            and $file =~ /~/ )
        {
            Warn("Not writing $path\\n");
            WarnOnce(
"Use -overwrite_original_in_place to write files with Unicode surrogate characters\\n"
            );
            EFile($file);
            ++$countBad;
            next;
        }
        $utf8FileName{$path} = 1 if $utf8Name;
        if ($list) {
            push( @$list, $path );
        }
        else {
            GetImageInfo( $et, $path );
            Image::ExifTool::Purge($purge) if $purge;
            if ($end) {
                Warn("End called - $file\\n");
                last;
            }
            if ($endDir) {
                $path =~ s(/$)();
                Warn("EndDir called - $path\\n");
                $endDir{$path} = 1;
                $endThisDir = 1;
                undef $endDir;
            }
        }
    }
    ++$countDir;
    $et->Options( CharsetFileName => $enc );
}

sub FindFileWindows($$) {
    my ( $et, $wildfile ) = @_;

    my $enc = $et->Options('CharsetFileName');
    $wildfile = $et->Decode( $wildfile, $enc, undef, 'UTF8' )
      if $enc and $enc ne 'UTF8';
    CleanFilename($wildfile);
    my ( $dir, $wildname ) =
      ( $wildfile =~ m{(.*[:/])(.*)} ) ? ( $1, $2 ) : ( '', $wildfile );
    if ( HasWildcards($dir) ) {
        Warn "Wildcards don't work in the directory specification\\n";
        return ();
    }
    CheckUTF8( $wildfile, $enc ) >= 0 or return ();
    undef $evalWarning;
    local $SIG{'__WARN__'} = sub { $evalWarning = $_[0] };
    my @files;
    eval {
        my @names = Win32::FindFile::FindFile($wildfile) or return;
        @names = sort { uc($a) cmp uc($b) } @names;
        my ( $rname, $nm );
        ( $rname = quotemeta $wildname ) =~ s/\\\\\\?/./g;
        $rname =~ s/\\\\\\*/.*/g;
        foreach $nm (@names) {
            $nm = $nm->cFileName;
            next unless $nm =~ /^$rname$/i;
            next if $nm eq '.' or $nm eq '..';
            my $file = "$dir$nm";
            push @files, $file;
            $utf8FileName{$file} = 1;
        }
    };
    $@ and $evalWarning = $@;
    if ($evalWarning) {
        chomp $evalWarning;
        $evalWarning =~ s/ at .*//s;
        Warn "Error: [Win32::FindFile] $evalWarning - $wildfile\\n";
        undef @files;
        EFile($wildfile);
        ++$countBad;
    }
    return @files;
}

sub FileNotFound($) {
    my $file = shift;
    if ( $file =~ /^(DIR|FILE)$/ ) {
        my $type = { DIR => 'directory', FILE => 'file' }->{$file};
        Warn
qq{You were meant to enter any valid $type name, not "$file" literally.\\n};
    }
}

sub PreserveTime() {
    local $_;
    $mt->SetFileTime( $_, @{ $preserveTime{$_} } ) foreach keys %preserveTime;
    undef %preserveTime;
}

sub AbsPath($) {
    my $file = shift;
    my $path;
    if ( defined $file ) {
        return undef if $file eq '*';
        if ( $^O eq 'MSWin32' and $mt->Options('WindowsLongPath') ) {
            $path = $mt->WindowsLongPath($file);
        }
        elsif ( eval { require Cwd } ) {
            local $SIG{'__WARN__'} = sub { };
            $path = eval { Cwd::abs_path($file) };
        }
        CleanFilename($path) if defined $path;
    }
    return $path;
}

sub MyConvertFileName($$) {
    my ( $et, $file ) = @_;
    my $enc = $et->Options('CharsetFileName');
    $et->Options( CharsetFileName => 'UTF8' ) if $utf8FileName{$file};
    my $convFile = $et->ConvertFileName($file);
    $et->Options( CharsetFileName => $enc ) if $utf8FileName{$file};
    return $convFile;
}

sub AddPrintFormat($) {
    my $expr = shift;
    my $type;
    if ( $expr =~ /^#/ ) {
        $expr =~ s/^#\\[(HEAD|SECT|IF|BODY|ENDS|TAIL)\\]// or return;
        $type = $1;
    }
    else {
        $type = 'BODY';
    }
    $printFmt{$type} or $printFmt{$type} = [];
    push @{ $printFmt{$type} }, $expr;
    push @requestTags, $expr =~ /\\$\\{?((?:[-_0-9A-Z]+:)*[-_0-9A-Z?*]+)/ig;
    $printFmt{SetTags} = 1 if $expr =~ /\\bSetTags\\b/;
}

sub SuggestedExtension($$$) {
    my ( $et, $valPt, $tag ) = @_;
    my $ext;
    if ( not $binaryOutput ) {
        $ext = 'txt';
    }
    elsif ( $$valPt =~ /^\\xff\\xd8\\xff/ ) {
        $ext = 'jpg';
    }
    elsif ( $$valPt =~
        /^(\\0\\0\\0\\x0cjP(  |\\x1a\\x1a)\\x0d\\x0a\\x87\\x0a|\\xff\\x4f\\xff\\x51\\0)/ )
    {
        $ext = 'jp2';
    }
    elsif ( $$valPt =~ /^(\\x89P|\\x8aM|\\x8bJ)NG\\r\\n\\x1a\\n/ ) {
        $ext = 'png';
    }
    elsif ( $$valPt =~ /^GIF8[79]a/ ) {
        $ext = 'gif';
    }
    elsif ( $$valPt =~ /^<\\?xpacket/ or $tag eq 'XMP' ) {
        $ext = 'xmp';
    }
    elsif ( $$valPt =~ /^<\\?xml/ or $tag eq 'XML' ) {
        $ext = 'xml';
    }
    elsif ( $$valPt =~ /^RIFF....WAVE/s ) {
        $ext = 'wav';
    }
    elsif ( $tag eq 'OriginalRawImage'
        and defined( $ext = $et->GetValue('OriginalRawFileName') ) )
    {
        $ext =~ s/^.*\\.//s;
        $ext = $ext ? lc($ext) : 'raw';
    }
    elsif ( $tag eq 'EXIF' ) {
        $ext = 'exif';
    }
    elsif ( $tag eq 'ICC_Profile' ) {
        $ext = 'icc';
    }
    elsif ( $$valPt =~ /^(MM\\0\\x2a|II\\x2a\\0)/ ) {
        $ext = 'tiff';
    }
    elsif ( $$valPt =~ /^.{4}ftyp(3gp|mp4|f4v|qt  )/s ) {
        my %movType = ( 'qt  ' => 'mov' );
        $ext = $movType{$1} || $1;
    }
    elsif ( $$valPt =~ /^<(!DOCTYPE )?html/i ) {
        $ext = 'html';
    }
    elsif ( $$valPt =~ /^[\\n\\r]*\\{[\\n\\r]*\\\\rtf/ ) {
        $ext = 'rtf';
    }
    elsif ( $$valPt !~ /^.{0,4096}\\0/s ) {
        $ext = 'txt';
    }
    elsif ( $$valPt =~ /^BM.{15}\\0/s ) {
        $ext = 'bmp';
    }
    elsif ( $$valPt =~ /^CANON OPTIONAL DATA\\0/ ) {
        $ext = 'vrd';
    }
    elsif ( $$valPt =~ /^IIII\\x04\\0\\x04\\0/ ) {
        $ext = 'dr4';
    }
    elsif ( $$valPt =~ /^(.{10}|.{522})(\\x11\\x01|\\x00\\x11)/s ) {
        $ext = 'pict';
    }
    elsif ( $$valPt =~ /^\\xff\\x0a|\\0\\0\\0\\x0cJXL \\x0d\\x0a......ftypjxl/s ) {
        $ext = 'jxl';
    }
    elsif ( $$valPt =~ /^.{4}jumb\\0.{3}jumdc2pa/s ) {
        $ext = 'c2pa';
    }
    elsif ( $tag eq 'JUMBF' ) {
        $ext = 'jumbf';
    }
    else {
        $ext = 'dat';
    }
    return $ext;
}

sub LoadPrintFormat($;$) {
    my ( $arg, $noNL ) = @_;
    if ( not defined $arg ) {
        Error "Must specify file or expression for -p option\\n";
    }
    elsif ( $arg !~ /\\n/ and -f $arg and $mt->Open( \\*FMT_FILE, $arg ) ) {
        foreach (<FMT_FILE>) {
            AddPrintFormat($_);
        }
        close(FMT_FILE);
    }
    else {
        $arg .= "\\n" unless $noNL;
        AddPrintFormat($arg);
    }
}

sub FilenameSPrintf($;$@) {
    my ( $fmt, $file, @extra ) = @_;
    local $_;
    return $fmt  unless $fmt =~ /%[-+]?\\d*[.:]?\\d*[lu]?[dDfFeEtgso]/;
    return undef unless defined $file;
    CleanFilename($file);

    my %part;
    @part{qw(d f E)} = ( $file =~ /^(.*?)([^\\/]*?)(\\.[^.\\/]*)?$/ );
    defined $part{f}
      or Warn("Error: Bad pattern match for file $file\\n"), return undef;
    if ( $part{E} ) {
        $part{e} = substr( $part{E}, 1 );
    }
    else {
        @part{qw(e E)} = ( '', '' );
    }
    $part{F} = $part{f} . $part{E};
    ( $part{D} = $part{d} ) =~ s{/+$}{};
    @part{qw(t g s o)} = @extra;
    $part{o} =~ s(^.*[/\\\\])()s if $part{o};
    my ( $filename, $pos ) = ( '', 0 );
    while ( $fmt =~ /(%([-+]?)(\\d*)([.:]?)(\\d*)([lu]?)([dDfFeEtgso]))/g ) {
        $filename .= substr( $fmt, $pos, pos($fmt) - $pos - length($1) );
        $pos = pos($fmt);
        my ( $sign, $wid, $dot, $skip, $mod, $code ) =
          ( $2, $3, $4, $5 || 0, $6, $7 );
        my ( @path, $part, $len, $groups );
        if ( lc $code eq 'd' and $dot and $dot eq ':' ) {
            @path = split '/', $part{$code};
            $len  = scalar @path;
        }
        else {
            if ( $code eq 'g' ) {
                $groups = $part{g} || [] unless defined $groups;
                $fmt =~ /\\G(\\d?)/g;
                $part{g} = $$groups[ $1 || 0 ];
                $pos = pos($fmt);
            }
            $part{$code} = '' unless defined $part{$code};
            $len = length $part{$code};
        }
        next unless $skip < $len;
        $wid  = $len - $skip        if $wid eq '' or $wid + $skip > $len;
        $skip = $len - $wid - $skip if $sign eq '-';
        if (@path) {
            $part = join( '/', @path[ $skip .. ( $skip + $wid - 1 ) ] );
            $part .= '/' unless $code eq 'D';
        }
        else {
            $part = substr( $part{$code}, $skip, $wid );
        }
        $part = ( $mod eq 'u' ) ? uc($part) : lc($part) if $mod;
        $filename .= $part;
    }
    $filename .= substr( $fmt, $pos );

    $filename =~ s{(?!^)//}{/}g;
    return $filename;
}

sub Num2Alpha($) {
    my $num   = shift;
    my $alpha = chr( 97 + ( $num % 26 ) );
    while ( $num >= 26 ) {
        $num   = int( $num / 26 ) - 1;
        $alpha = chr( 97 + ( $num % 26 ) ) . $alpha;
    }
    return $alpha;
}

sub NextUnusedFilename($;$) {
    my ( $fmt, $okfile ) = @_;
    return $fmt unless $fmt =~ /%[-+]?\\d*[.:]?\\d*[lun]?[cC]/;
    my %sep = ( '-' => '-', '+' => '_' );
    my ( $copy, $alpha ) = ( 0, 'a' );
    my $lastFile;
    for ( ; ; ) {
        my ( $filename, $pos ) = ( '', 0 );
        while ( $fmt =~ /(%([-+]?)(\\d*)([.:]?)(\\d*)([lun]?)([cC]))/g ) {
            $filename .= substr( $fmt, $pos, pos($fmt) - $pos - length($1) );
            $pos = pos($fmt);
            my ( $sign, $wid, $dec, $wid2, $mod, $tok ) =
              ( $2, $3 || 0, $4, $5 || 0, $6, $7 );
            my $seq;
            if ( $tok eq 'C' ) {
                $sign eq '-' ? ++$seqFileDir : ++$seqFileNum
                  if $copy and $dec eq ':';
                $seq = $wid + ( $sign eq '-' ? $seqFileDir : $seqFileNum ) - 1;
                $wid = $wid2;
            }
            else {
                next unless $dec or $copy;
                $wid = $wid2 if $wid < $wid2;
                $filename .= $sep{$sign} if $sign;
            }
            if ( $mod and $mod ne 'n' ) {
                my $a = $tok eq 'C' ? Num2Alpha($seq) : $alpha;
                my $str =
                  ( $wid and $wid > length $a )
                  ? 'a' x ( $wid - length($a) )
                  : '';
                $str .= $a;
                $str = uc $str if $mod eq 'u';
                $filename .= $str;
            }
            else {
                my $c   = $tok eq 'C' ? $seq : $copy;
                my $num = $c + ( $mod ? 1 : 0 );
                $filename .= $wid ? sprintf( "%.\${wid}d", $num ) : $num;
            }
        }
        $filename .= substr( $fmt, $pos );

        return $filename
          unless ( $mt->Exists( $filename, 1 )
            and not defined $usedFileName{$filename} )
          or $usedFileName{$filename};
        if ( defined $okfile ) {
            return $filename if $filename eq $okfile;
            my ( $fn, $ok ) = ( AbsPath($filename), AbsPath($okfile) );
            return $okfile if defined $fn and defined $ok and $fn eq $ok;
        }
        return $filename if defined $lastFile and $lastFile eq $filename;
        $lastFile = $filename;
        ++$copy;
        ++$alpha;
    }
}

sub CreateDirectory($) {
    my $file = shift;
    my $err  = $mt->CreateDirectory($file);
    if ( defined $err ) {
        $err and Error("$err\\n"), return 0;
        if ($verbose) {
            my $dir;
            ( $dir = $file ) =~ s(/[^/]*$)();
            print $vout "Created directory $dir\\n";
        }
        ++$countNewDir;
        return 1;
    }
    return 0;
}

sub OpenOutputFile($;@) {
    my ( $file, @args ) = @_;
    my ( $fp, $outfile, $append );
    if ($textOut) {
        $outfile = $file;
        CleanFilename($outfile);
        if ( $textOut =~ /%[-+]?\\d*[.:]?\\d*[lun]?[dDfFeEtgsocC]/
            or defined $tagOut )
        {
            $outfile = FilenameSPrintf( $textOut, $file, @args );
            return () unless defined $outfile;
            $outfile = NextUnusedFilename($outfile);
            CreateDirectory($outfile);
        }
        else {
            $outfile =~ s/\\.[^.\\/]*$//;
            $outfile .= $textOut;
        }
        my $mode = '>';
        if ( $mt->Exists( $outfile, 1 ) ) {
            unless ($textOverwrite) {
                Warn "Output file $outfile already exists for $file\\n";
                return ();
            }
            if ( $textOverwrite == 2
                or ( $textOverwrite == 3 and $created{$outfile} ) )
            {
                $mode   = '>>';
                $append = 1;
            }
        }
        unless ( $mt->Open( \\*OUTFILE, $outfile, $mode ) ) {
            my $what = $mode eq '>' ? 'creating' : 'appending to';
            Error("Error $what $outfile\\n");
            return ();
        }
        binmode(OUTFILE) if $binaryOutput;
        $fp = \\*OUTFILE;
    }
    else {
        $fp = \\*STDOUT;
    }
    return ( $fp, $outfile, $append );
}

sub AcceptFile($) {
    my $file = shift;
    my $ext  = ( $file =~ /^.*\\.(.+)$/s ) ? uc($1) : '';
    return $filterExt{$ext} if defined $filterExt{$ext};
    return $filterExt{'*'}  if defined $filterExt{'*'};
    return 0                if $filterFlag & 0x02;
    return 2;
}

sub SlurpFile($$) {
    my ( $file, $buffPt ) = @_;
    $mt->Open( \\*INFILE, $file )
      or Warn("Error opening file $file\\n"), return 0;
    binmode(INFILE);
    undef $$buffPt;
    my $bsize = 1024 * 1024;
    my $num   = read( INFILE, $$buffPt, $bsize );
    unless ( defined $num ) {
        close(INFILE);
        Warn("Error reading $file\\n");
        return 0;
    }
    my $bmax = 64 * $bsize;
    while ( $num == $bsize ) {
        $bsize *= 2 if $bsize < $bmax;
        my $buff;
        $num = read( INFILE, $buff, $bsize );
        last unless $num;
        $$buffPt .= $buff;
    }
    close(INFILE);
    return 1;
}

sub FilterArgfileLine($) {
    my $arg = shift;
    if ( $arg =~ /^#/ ) {
        return undef unless $arg =~ s/^#\\[CSTR\\]//;
        $arg =~ s/[\\x0d\\x0a]+$//s;

        $arg =~ s{\\\\(.)|(["\\$\\@]|\\\\$)}{'\\\\'.($2 || $1)}sge;
        my %esc = (
            a    => "\\a",
            b    => "\\b",
            f    => "\\f",
            n    => "\\n",
            r    => "\\r",
            t    => "\\t",
            '"'  => '"',
            '\\\\' => '\\\\'
        );
        $arg =~ s/\\\\(.)/$esc{$1}||'\\\\'.$1/egs;
    }
    else {
        $arg =~ s/^\\s+//;
        $arg =~ s/[\\x0d\\x0a]+$//s;

        $arg =~ s/^(-[-_0-9A-Z:]+#?)\\s*([-+<]?=) ?/$1$2/i;
        return undef if $arg eq '';
    }
    return $arg;
}

sub ReadStayOpen($) {
    my $args = shift;
    my ( @newArgs, $processArgs, $result, $optArgs );
    my $lastOpt  = '';
    my $unparsed = length $stayOpenBuff;
    for ( ; ; ) {
        if ($unparsed) {
            $result = $unparsed;
            undef $unparsed;
        }
        else {
            $result =
              sysread( STAYOPEN, $stayOpenBuff, 65536, length($stayOpenBuff) );
        }
        if ($result) {
            my $pos = 0;
            while ( $stayOpenBuff =~ /\\n/g ) {
                my $len = pos($stayOpenBuff) - $pos;
                my $arg = substr( $stayOpenBuff, $pos, $len );
                $pos += $len;
                $arg = FilterArgfileLine($arg);
                next unless defined $arg;
                push @newArgs, $arg;
                if ($optArgs) {
                    undef $optArgs;
                    next unless $lastOpt eq '-stay_open' or $lastOpt eq '-@';
                }
                else {
                    $lastOpt = lc $arg;
                    $optArgs = $optArgs{$arg};
                    unless ( defined $optArgs ) {
                        $optArgs = $optArgs{$lastOpt};
                        $optArgs = $optArgs{"$1#$2"}
                          if not defined $optArgs
                          and $lastOpt =~ /^(.*?)\\d+(!?)$/;
                    }
                    next unless $lastOpt =~ /^-execute\\d*$/;
                }
                $processArgs = 1;
                last;
            }
            next unless $pos;

            $stayOpenBuff = substr( $stayOpenBuff, $pos );
            if ($processArgs) {
                unshift @$args, @newArgs;
                last;
            }
        }
        elsif ( $result == 0 ) {
            select( undef, undef, undef, 0.01 );
        }
        else {
            Warn "Error reading from ARGFILE\\n";
            close STAYOPEN;
            $stayOpen = 0;
            last;
        }
    }
}

sub EFile($$) {
    my $entry = shift;
    my $efile = $efile[ shift || 0 ];
    if ( defined $efile and length $entry and $entry ne '-' ) {
        my $err;
        CreateDirectory($efile);
        if ( $mt->Open( \\*EFILE_FILE, $efile, '>>' ) ) {
            print EFILE_FILE $entry, "\\n"
              or Warn("Error writing to $efile\\n"), $err = 1;
            close EFILE_FILE;
        }
        else {
            Warn("Error opening '\${efile}' for append\\n");
            $err = 1;
        }
        if ($err) {
            defined $_ and $_ eq $efile and undef $_ foreach @efile;
        }
    }
}

sub Progress($$) {
    my ( $file, $msg ) = @_;
    if ( defined $progStr ) {
        print $file $msg, $progStr, "\\n";
        undef $progressNext if defined $progressMax;
    }
}

sub PrintTagList($@) {
    my $msg = shift;
    print $msg, ":\\n" unless $quiet;
    my $tag;
    if ( ( $outFormat < 0 or $verbose ) and $msg =~ /file extensions$/ and @_ )
    {
        foreach $tag (@_) {
            printf( "  %-11s %s\\n", $tag, GetFileType( $tag, 1 ) );
        }
        return;
    }
    my ( $len, $pad ) = ( 0, $quiet ? '' : '  ' );
    foreach $tag (@_) {
        my $taglen = length($tag);
        if ( $len + $taglen > 77 ) {
            print "\\n";
            ( $len, $pad ) = ( 0, $quiet ? '' : '  ' );
        }
        print $pad, $tag;
        $len += $taglen + 1;
        $pad = ' ';
    }
    @_ or print $pad, '[empty list]';
    print "\\n";
}

sub PrintErrors($$$) {
    my ( $et, $info, $file ) = @_;
    my ( $tag, $key );
    foreach $tag (qw(Warning Error)) {
        next unless $$info{$tag};
        my @keys = ($tag);
        push @keys, sort( grep /^$tag /, keys %$info )
          if $et->Options('Duplicates');
        foreach $key (@keys) {
            Warn "$tag: $info->{$key} - $file\\n";
        }
    }
    return $$info{Error};
}

__END__


#------------------------------------------------------------------------------
# end
`;class G{chunks=[];decoder=new TextDecoder("utf-8");encoder=new TextEncoder;append(e){return this.chunks.push(typeof e==="string"?this.encoder.encode(e):e),this}clear(){return this.chunks=[],this}get byteLength(){return this.chunks.reduce((e,a)=>e+a.byteLength,0)}toBytes(){let e=new Uint8Array(this.byteLength),a=0;for(let r of this.chunks)e.set(r,a),a+=r.byteLength;return e}toString(){return this.decoder.decode(this.toBytes())}}var O=null,X=null,M=new G,B=new G;async function Ae(e){let a=O?.deref(),r=X?.deref();if(a&&r)return{perl:a,fileSystem:r};return r=new I({"/":""}),r.addFile("/exiftool",ie),a=await R.create({fileSystem:r,outputBuffers:!0,stdout:(t)=>{M.append(t)},stderr:(t)=>{B.append(t)},fetch:e}),O=new WeakRef(a),X=new WeakRef(r),{perl:a,fileSystem:r}}function se(e,a){for(let r of a)try{e.removeFile(r)}catch{}}function Ce(e){return Object.entries(e).flatMap(([a,r])=>Array.isArray(r)?r.map((t)=>`-${a}=${t}`):[`-${a}=${r}`])}async function oa(e,a={}){let{perl:r,fileSystem:t}=await Ae(a.fetch),n=[];M.clear(),B.clear(),await r.reset();try{let name=(e&&e.name)||(e&&e.data&&e.data.name)||"image.jpg";let o=`/${name}`;if(e instanceof File||e instanceof Blob)t.addFile(o,e);else t.addFile(o,e&&e.data!==undefined?e.data:e);n.push(o);let s=[];if(a.config){let A=`/${a.config.name}`;if(a.config instanceof File)t.addFile(A,a.config);else t.addFile(A,a.config.data);n.push(A),s.push("-config",A)}s.push(...a.args||[]),s.push(o);let l=await r.runFile("/exiftool",s);r.flush();let c=B.toString();if(!l.success||l.exitCode!==0)return{success:!1,data:void 0,error:r.getLastError()||c||"Unknown error",exitCode:l.exitCode};if(a.binary){if(M.byteLength>0)return{success:!0,data:M.toBytes(),exitCode:0};return{success:!1,data:void 0,error:c||"No output data from ExifTool",exitCode:0}}let m=M.toString();if(m&&m.trim()){let S;if(a.transform)S=a.transform(m);else S=m;return{success:!0,data:S,exitCode:0}}if(c&&c.trim())return{success:!1,data:void 0,error:c,exitCode:0};return{success:!1,data:void 0,error:"No output data from ExifTool",exitCode:0};}finally{se(t,n)}}async function ia(e,a,r={}){let{perl:t,fileSystem:n}=await Ae(r.fetch),o=[];M.clear(),B.clear(),await t.reset();try{let name=(e&&e.name)||(e&&e.data&&e.data.name)||"image.jpg";let s=`/${name}`;if(e instanceof File||e instanceof Blob)n.addFile(s,e);else n.addFile(s,e&&e.data!==undefined?e.data:e);o.push(s);let l=[];if(r.config){let f=`/${r.config.name}`;if(r.config instanceof File)n.addFile(f,r.config);else n.addFile(f,r.config.data);o.push(f),l.push("-config",f)}l.push(...r.args||[]),l.push(...Ce(a));let c=`/${crypto.randomUUID().replace(/-/g,"")}.tmp`;o.push(c),l.push("-o",c),l.push(s);let m=await t.runFile("/exiftool",l);t.flush();let S=B.toString();if(!m.success||m.exitCode!==0)return{success:!1,data:void 0,error:t.getLastError()||S||"Unknown error",exitCode:m.exitCode};if(S&&S.trim())return{success:!1,data:void 0,error:S,exitCode:0};let A=n.lookup(c);if(!A||A.type!=="file")return{success:!1,data:void 0,error:`Temporary output file not found: ${c}`,exitCode:0};return{success:!0,data:A.content instanceof Blob?await A.content.arrayBuffer():A.content.buffer,exitCode:0}}finally{se(n,o)}}async function Aa(){let e=O?.deref();if(e)e.dispose(),O=null,X=null}export{ia as writeMetadata,oa as parseMetadata,Aa as dispose};

//# debugId=7B0280D2AC35E5EE64756E2164756E21
//# sourceMappingURL=data:application/json;base64,ewogICJ2ZXJzaW9uIjogMywKICAic291cmNlcyI6IFsiLi4vLi4vc3JjL3plcm9wZXJsL3dhc2kvYWJpLnRzIiwgIi4uLy4uL3NyYy96ZXJvcGVybC93YXNpL2ZlYXR1cmVzL2FyZ3MudHMiLCAiLi4vLi4vc3JjL3plcm9wZXJsL3dhc2kvZmVhdHVyZXMvY2xvY2sudHMiLCAiLi4vLi4vc3JjL3plcm9wZXJsL3dhc2kvZmVhdHVyZXMvZW52aXJvbi50cyIsICIuLi8uLi9zcmMvemVyb3Blcmwvd2FzaS9mZWF0dXJlcy9mZC50cyIsICIuLi8uLi9zcmMvemVyb3Blcmwvd2FzaS9mZWF0dXJlcy9wcm9jLnRzIiwgIi4uLy4uL3NyYy96ZXJvcGVybC93YXNpL2ZlYXR1cmVzL3JhbmRvbS50cyIsICIuLi8uLi9zcmMvemVyb3Blcmwvd2FzaS9pbmRleC50cyIsICIuLi8uLi9zcmMvemVyb3Blcmwvd2FzaS9hc3luY2lmeS50cyIsICIuLi8uLi9zcmMvemVyb3BlcmwvaW5kZXgudHMiLCAiLi4vLi4vc3JjL3NiLnRzIiwgIi4uLy4uL3NyYy9pbmRleC50cyJdLAogICJzb3VyY2VzQ29udGVudCI6IFsKICAgICJleHBvcnQgY2xhc3MgV0FTSUFiaSB7XG4gICAgLyoqXG4gICAgICogTm8gZXJyb3Igb2NjdXJyZWQuIFN5c3RlbSBjYWxsIGNvbXBsZXRlZCBzdWNjZXNzZnVsbHkuXG4gICAgICovXG4gICAgc3RhdGljIHJlYWRvbmx5IFdBU0lfRVNVQ0NFU1MgPSAwO1xuXG4gICAgLyoqXG4gICAgICogQmFkIGZpbGUgZGVzY3JpcHRvci5cbiAgICAgKi9cbiAgICBzdGF0aWMgcmVhZG9ubHkgV0FTSV9FUlJOT19CQURGID0gODtcblxuICAgIC8qKlxuICAgICAqIEZ1bmN0aW9uIG5vdCBzdXBwb3J0ZWQuXG4gICAgICovXG4gICAgc3RhdGljIHJlYWRvbmx5IFdBU0lfRU5PU1lTID0gNTI7XG5cbiAgICAvKipcbiAgICAgKiBUaGUgY2xvY2sgbWVhc3VyaW5nIHJlYWwgdGltZS4gVGltZSB2YWx1ZSB6ZXJvIGNvcnJlc3BvbmRzIHdpdGggMTk3MC0wMS0wMVQwMDowMDowMFouXG4gICAgICovXG4gICAgc3RhdGljIHJlYWRvbmx5IFdBU0lfQ0xPQ0tfUkVBTFRJTUUgPSAwO1xuICAgIC8qKlxuICAgICAqIFRoZSBzdG9yZS13aWRlIG1vbm90b25pYyBjbG9jaywgd2hpY2ggaXMgZGVmaW5lZCBhcyBhIGNsb2NrIG1lYXN1cmluZyByZWFsIHRpbWUsXG4gICAgICogd2hvc2UgdmFsdWUgY2Fubm90IGJlIGFkanVzdGVkIGFuZCB3aGljaCBjYW5ub3QgaGF2ZSBuZWdhdGl2ZSBjbG9jayBqdW1wcy5cbiAgICAgKiBUaGUgZXBvY2ggb2YgdGhpcyBjbG9jayBpcyB1bmRlZmluZWQuIFRoZSBhYnNvbHV0ZSB0aW1lIHZhbHVlIG9mIHRoaXMgY2xvY2sgdGhlcmVmb3JlIGhhcyBubyBtZWFuaW5nLlxuICAgICAqL1xuICAgIHN0YXRpYyByZWFkb25seSBXQVNJX0NMT0NLX01PTk9UT05JQyA9IDE7XG5cbiAgICAvKipcbiAgICAgKiBUaGUgZmlsZSBkZXNjcmlwdG9yIG9yIGZpbGUgcmVmZXJzIHRvIGEgZGlyZWN0b3J5LlxuICAgICAqL1xuICAgIHN0YXRpYyByZWFkb25seSBXQVNJX0VSUk5PX0lTRElSID0gMzE7XG4gICAgLyoqXG4gICAgICogSW52YWxpZCBhcmd1bWVudC5cbiAgICAgKi9cbiAgICBzdGF0aWMgcmVhZG9ubHkgV0FTSV9FUlJOT19JTlZBTCA9IDI4O1xuICAgIC8qKlxuICAgICAqIE5vdCBhIGRpcmVjdG9yeSBvciBhIHN5bWJvbGljIGxpbmsgdG8gYSBkaXJlY3RvcnkuXG4gICAgICovXG4gICAgc3RhdGljIHJlYWRvbmx5IFdBU0lfRVJSTk9fTk9URElSID0gNTQ7XG4gICAgLyoqXG4gICAgICogTm8gc3VjaCBmaWxlIG9yIGRpcmVjdG9yeS5cbiAgICAgKi9cbiAgICBzdGF0aWMgcmVhZG9ubHkgV0FTSV9FUlJOT19OT0VOVCA9IDQ0O1xuICAgIC8qKlxuICAgICAqIEZpbGUgZXhpc3RzLlxuICAgICAqL1xuICAgIHN0YXRpYyByZWFkb25seSBXQVNJX0VSUk5PX0VYSVNUID0gMjA7XG4gICAgLyoqXG4gICAgICogSS9PIGVycm9yLlxuICAgICAqL1xuICAgIHN0YXRpYyByZWFkb25seSBXQVNJX0VSUk5PX0lPID0gMjk7XG5cbiAgICAvKipcbiAgICAgKiBUaGUgZmlsZSBkZXNjcmlwdG9yIG9yIGZpbGUgcmVmZXJzIHRvIGEgY2hhcmFjdGVyIGRldmljZSBpbm9kZS5cbiAgICAgKi9cbiAgICBzdGF0aWMgcmVhZG9ubHkgV0FTSV9GSUxFVFlQRV9DSEFSQUNURVJfREVWSUNFID0gMjtcbiAgICAvKipcbiAgICAgKiBUaGUgZmlsZSBkZXNjcmlwdG9yIG9yIGZpbGUgcmVmZXJzIHRvIGEgZGlyZWN0b3J5IGlub2RlLlxuICAgICAqL1xuICAgIHN0YXRpYyByZWFkb25seSBXQVNJX0ZJTEVUWVBFX0RJUkVDVE9SWSA9IDM7XG4gICAgLyoqXG4gICAgICogVGhlIGZpbGUgZGVzY3JpcHRvciBvciBmaWxlIHJlZmVycyB0byBhIHJlZ3VsYXIgZmlsZSBpbm9kZS5cbiAgICAgKi9cbiAgICBzdGF0aWMgcmVhZG9ubHkgV0FTSV9GSUxFVFlQRV9SRUdVTEFSX0ZJTEUgPSA0O1xuXG5cbiAgICBzdGF0aWMgcmVhZG9ubHkgSU1QT1JUX0ZVTkNUSU9OUyA9IFtcbiAgICAgICAgXCJhcmdzX2dldFwiLFxuICAgICAgICBcImFyZ3Nfc2l6ZXNfZ2V0XCIsXG5cbiAgICAgICAgXCJjbG9ja19yZXNfZ2V0XCIsXG4gICAgICAgIFwiY2xvY2tfdGltZV9nZXRcIixcblxuICAgICAgICBcImVudmlyb25fZ2V0XCIsXG4gICAgICAgIFwiZW52aXJvbl9zaXplc19nZXRcIixcblxuICAgICAgICBcImZkX2FkdmlzZVwiLFxuICAgICAgICBcImZkX2FsbG9jYXRlXCIsXG4gICAgICAgIFwiZmRfY2xvc2VcIixcbiAgICAgICAgXCJmZF9kYXRhc3luY1wiLFxuICAgICAgICBcImZkX2Zkc3RhdF9nZXRcIixcbiAgICAgICAgXCJmZF9mZHN0YXRfc2V0X2ZsYWdzXCIsXG4gICAgICAgIFwiZmRfZmRzdGF0X3NldF9yaWdodHNcIixcbiAgICAgICAgXCJmZF9maWxlc3RhdF9nZXRcIixcbiAgICAgICAgXCJmZF9maWxlc3RhdF9zZXRfc2l6ZVwiLFxuICAgICAgICBcImZkX2ZpbGVzdGF0X3NldF90aW1lc1wiLFxuICAgICAgICBcImZkX3ByZWFkXCIsXG4gICAgICAgIFwiZmRfcHJlc3RhdF9kaXJfbmFtZVwiLFxuICAgICAgICBcImZkX3ByZXN0YXRfZ2V0XCIsXG4gICAgICAgIFwiZmRfcHdyaXRlXCIsXG4gICAgICAgIFwiZmRfcmVhZFwiLFxuICAgICAgICBcImZkX3JlYWRkaXJcIixcbiAgICAgICAgXCJmZF9yZW51bWJlclwiLFxuICAgICAgICBcImZkX3NlZWtcIixcbiAgICAgICAgXCJmZF9zeW5jXCIsXG4gICAgICAgIFwiZmRfdGVsbFwiLFxuICAgICAgICBcImZkX3dyaXRlXCIsXG5cbiAgICAgICAgXCJwYXRoX2NyZWF0ZV9kaXJlY3RvcnlcIixcbiAgICAgICAgXCJwYXRoX2ZpbGVzdGF0X2dldFwiLFxuICAgICAgICBcInBhdGhfZmlsZXN0YXRfc2V0X3RpbWVzXCIsXG4gICAgICAgIFwicGF0aF9saW5rXCIsXG4gICAgICAgIFwicGF0aF9vcGVuXCIsXG4gICAgICAgIFwicGF0aF9yZWFkbGlua1wiLFxuICAgICAgICBcInBhdGhfcmVtb3ZlX2RpcmVjdG9yeVwiLFxuICAgICAgICBcInBhdGhfcmVuYW1lXCIsXG4gICAgICAgIFwicGF0aF9zeW1saW5rXCIsXG4gICAgICAgIFwicGF0aF91bmxpbmtfZmlsZVwiLFxuXG4gICAgICAgIFwicG9sbF9vbmVvZmZcIixcblxuICAgICAgICBcInByb2NfZXhpdFwiLFxuICAgICAgICBcInByb2NfcmFpc2VcIixcblxuICAgICAgICBcInJhbmRvbV9nZXRcIixcblxuICAgICAgICBcInNjaGVkX3lpZWxkXCIsXG5cbiAgICAgICAgXCJzb2NrX2FjY2VwdFwiLFxuICAgICAgICBcInNvY2tfcmVjdlwiLFxuICAgICAgICBcInNvY2tfc2VuZFwiLFxuICAgICAgICBcInNvY2tfc2h1dGRvd25cIixcbiAgICBdO1xuXG4gICAgcHJpdmF0ZSBlbmNvZGVyOiBUZXh0RW5jb2RlcjtcbiAgICBwcml2YXRlIGRlY29kZXI6IFRleHREZWNvZGVyO1xuXG4gICAgY29uc3RydWN0b3IoKSB7XG4gICAgICAgIHRoaXMuZW5jb2RlciA9IG5ldyBUZXh0RW5jb2RlcigpO1xuICAgICAgICB0aGlzLmRlY29kZXIgPSBuZXcgVGV4dERlY29kZXIoKTtcbiAgICB9XG5cblxuICAgIHN0cmluZ0FycmF5U2l6ZShzdHJpbmdzOiBzdHJpbmdbXSk6IHtcbiAgICAgICAgcG9pbnRlckFycmF5U2l6ZTogbnVtYmVyO1xuICAgICAgICBidWZmZXJTaXplOiBudW1iZXI7XG4gICAgICAgIHRvdGFsU2l6ZTogbnVtYmVyXG4gICAgfSB7XG4gICAgICAgIGNvbnN0IHBvaW50ZXJBcnJheVNpemUgPSBzdHJpbmdzLmxlbmd0aCAqIDQ7IC8vIDQgYnl0ZXMgcGVyIHBvaW50ZXIgKHUzMilcbiAgICAgICAgY29uc3QgYnVmZmVyU2l6ZSA9IHN0cmluZ3MucmVkdWNlKChhY2MsIHN0cikgPT4gYWNjICsgdGhpcy5ieXRlTGVuZ3RoKHN0cikgKyAxLCAwKTsgLy8gKzEgZm9yIG51bGwgdGVybWluYXRvclxuICAgICAgICByZXR1cm4ge1xuICAgICAgICAgICAgcG9pbnRlckFycmF5U2l6ZSxcbiAgICAgICAgICAgIGJ1ZmZlclNpemUsXG4gICAgICAgICAgICB0b3RhbFNpemU6IHBvaW50ZXJBcnJheVNpemUgKyBidWZmZXJTaXplXG4gICAgICAgIH07XG4gICAgfVxuXG4gICAgd3JpdGVTdHJpbmdBcnJheShcbiAgICAgICAgbWVtb3J5OiBEYXRhVmlldyxcbiAgICAgICAgc3RyaW5nczogc3RyaW5nW10sXG4gICAgICAgIGFycmF5T2Zmc2V0OiBudW1iZXIsXG4gICAgICAgIGJ1ZmZlck9mZnNldDogbnVtYmVyXG4gICAgKTogbnVtYmVyIHtcbiAgICAgICAgbGV0IGN1cnJlbnRBcnJheU9mZnNldCA9IGFycmF5T2Zmc2V0O1xuICAgICAgICBsZXQgY3VycmVudEJ1ZmZlck9mZnNldCA9IGJ1ZmZlck9mZnNldDtcblxuICAgICAgICBmb3IgKGNvbnN0IHN0ciBvZiBzdHJpbmdzKSB7XG4gICAgICAgICAgICBtZW1vcnkuc2V0VWludDMyKGN1cnJlbnRBcnJheU9mZnNldCwgY3VycmVudEJ1ZmZlck9mZnNldCwgdHJ1ZSk7XG4gICAgICAgICAgICBjdXJyZW50QXJyYXlPZmZzZXQgKz0gNDtcblxuICAgICAgICAgICAgY3VycmVudEJ1ZmZlck9mZnNldCArPSB0aGlzLndyaXRlU3RyaW5nKG1lbW9yeSwgYCR7c3RyfVxcMGAsIGN1cnJlbnRCdWZmZXJPZmZzZXQpO1xuICAgICAgICB9XG5cbiAgICAgICAgcmV0dXJuIGN1cnJlbnRCdWZmZXJPZmZzZXQgLSBidWZmZXJPZmZzZXQ7XG4gICAgfVxuICAgIHdyaXRlU3RyaW5nKG1lbW9yeTogRGF0YVZpZXcsIHZhbHVlOiBzdHJpbmcsIG9mZnNldDogbnVtYmVyKTogbnVtYmVyIHtcbiAgICAgICAgY29uc3QgYnl0ZXMgPSB0aGlzLmVuY29kZXIuZW5jb2RlKHZhbHVlKTtcbiAgICAgICAgY29uc3QgYnVmZmVyID0gbmV3IFVpbnQ4QXJyYXkobWVtb3J5LmJ1ZmZlciwgb2Zmc2V0LCBieXRlcy5sZW5ndGgpO1xuICAgICAgICBidWZmZXIuc2V0KGJ5dGVzKTtcbiAgICAgICAgcmV0dXJuIGJ5dGVzLmxlbmd0aDtcbiAgICB9XG5cbiAgICByZWFkU3RyaW5nKG1lbW9yeTogRGF0YVZpZXcsIHB0cjogbnVtYmVyLCBsZW46IG51bWJlcik6IHN0cmluZyB7XG4gICAgICAgIGNvbnN0IGJ1ZmZlciA9IG5ldyBVaW50OEFycmF5KG1lbW9yeS5idWZmZXIsIHB0ciwgbGVuKTtcbiAgICAgICAgcmV0dXJuIHRoaXMuZGVjb2Rlci5kZWNvZGUoYnVmZmVyKTtcbiAgICB9XG5cbiAgICBieXRlTGVuZ3RoKHZhbHVlOiBzdHJpbmcpOiBudW1iZXIge1xuICAgICAgICByZXR1cm4gdGhpcy5lbmNvZGVyLmVuY29kZSh2YWx1ZSkubGVuZ3RoO1xuICAgIH1cblxuICAgIHByaXZhdGUgc3RhdGljIHJlYWRvbmx5IGlvdmVjX3QgPSB7XG4gICAgICAgIHNpemU6IDgsXG4gICAgICAgIGJ1ZmZlck9mZnNldDogMCxcbiAgICAgICAgbGVuZ3RoT2Zmc2V0OiA0LFxuICAgIH07XG5cbiAgICBpb3ZWaWV3cyhtZW1vcnk6IERhdGFWaWV3LCBpb3ZzOiBudW1iZXIsIGlvdnNMZW46IG51bWJlcik6IFVpbnQ4QXJyYXlbXSB7XG4gICAgICAgIGNvbnN0IGlvdnNCdWZmZXJzOiBVaW50OEFycmF5W10gPSBbXTtcbiAgICAgICAgbGV0IGlvdnNPZmZzZXQgPSBpb3ZzO1xuXG4gICAgICAgIGZvciAobGV0IGkgPSAwOyBpIDwgaW92c0xlbjsgaSsrKSB7XG4gICAgICAgICAgICBjb25zdCBvZmZzZXQgPSBtZW1vcnkuZ2V0VWludDMyKFxuICAgICAgICAgICAgICAgIGlvdnNPZmZzZXQgKyBXQVNJQWJpLmlvdmVjX3QuYnVmZmVyT2Zmc2V0LFxuICAgICAgICAgICAgICAgIHRydWVcbiAgICAgICAgICAgICk7XG4gICAgICAgICAgICBjb25zdCBsZW4gPSBtZW1vcnkuZ2V0VWludDMyKFxuICAgICAgICAgICAgICAgIGlvdnNPZmZzZXQgKyBXQVNJQWJpLmlvdmVjX3QubGVuZ3RoT2Zmc2V0LFxuICAgICAgICAgICAgICAgIHRydWVcbiAgICAgICAgICAgICk7XG5cbiAgICAgICAgICAgIGlvdnNCdWZmZXJzLnB1c2gobmV3IFVpbnQ4QXJyYXkobWVtb3J5LmJ1ZmZlciwgb2Zmc2V0LCBsZW4pKTtcbiAgICAgICAgICAgIGlvdnNPZmZzZXQgKz0gV0FTSUFiaS5pb3ZlY190LnNpemU7XG4gICAgICAgIH1cbiAgICAgICAgcmV0dXJuIGlvdnNCdWZmZXJzO1xuICAgIH1cblxuICAgIHdyaXRlRmlsZXN0YXQoXG4gICAgICAgIG1lbW9yeTogRGF0YVZpZXcsXG4gICAgICAgIHB0cjogbnVtYmVyLFxuICAgICAgICBmaWxldHlwZTogbnVtYmVyLFxuICAgICAgICBzaXplOiBiaWdpbnQgPSAwbixcbiAgICAgICAgYXRpbTogYmlnaW50ID0gMG4sXG4gICAgICAgIG10aW06IGJpZ2ludCA9IDBuLFxuICAgICAgICBjdGltOiBiaWdpbnQgPSAwblxuICAgICk6IHZvaWQge1xuICAgICAgICBtZW1vcnkuc2V0QmlnVWludDY0KHB0ciwgLyogZGV2ICovIDBuLCB0cnVlKTtcbiAgICAgICAgbWVtb3J5LnNldEJpZ1VpbnQ2NChwdHIgKyA4LCAvKiBpbm8gKi8gMG4sIHRydWUpO1xuICAgICAgICBtZW1vcnkuc2V0VWludDgocHRyICsgMTYsIGZpbGV0eXBlKTtcbiAgICAgICAgbWVtb3J5LnNldEJpZ1VpbnQ2NChwdHIgKyAyNCwgLyogbmxpbmsgKi8gMW4sIHRydWUpO1xuICAgICAgICBtZW1vcnkuc2V0QmlnVWludDY0KHB0ciArIDMyLCAvKiBzaXplICovIHNpemUsIHRydWUpO1xuICAgICAgICBtZW1vcnkuc2V0QmlnVWludDY0KHB0ciArIDQwLCAvKiBhdGltICovIGF0aW0sIHRydWUpO1xuICAgICAgICBtZW1vcnkuc2V0QmlnVWludDY0KHB0ciArIDQ4LCAvKiBtdGltICovIG10aW0sIHRydWUpO1xuICAgICAgICBtZW1vcnkuc2V0QmlnVWludDY0KHB0ciArIDU2LCAvKiBjdGltICovIGN0aW0sIHRydWUpO1xuICAgIH1cblxuICAgIHdyaXRlRmRzdGF0KFxuICAgICAgICBtZW1vcnk6IERhdGFWaWV3LFxuICAgICAgICBwdHI6IG51bWJlcixcbiAgICAgICAgZmlsZXR5cGU6IG51bWJlcixcbiAgICAgICAgZmRmbGFnczogbnVtYmVyLFxuICAgICAgICByaWdodHNCYXNlOiBiaWdpbnQsXG4gICAgICAgIHJpZ2h0c0luaGVyaXRpbmc6IGJpZ2ludFxuICAgICk6IHZvaWQge1xuICAgICAgICBtZW1vcnkuc2V0VWludDgocHRyLCBmaWxldHlwZSk7XG4gICAgICAgIG1lbW9yeS5zZXRVaW50MTYocHRyICsgMiwgZmRmbGFncywgdHJ1ZSk7XG4gICAgICAgIG1lbW9yeS5zZXRCaWdVaW50NjQocHRyICsgOCwgcmlnaHRzQmFzZSwgdHJ1ZSk7XG4gICAgICAgIG1lbW9yeS5zZXRCaWdVaW50NjQocHRyICsgMTYsIHJpZ2h0c0luaGVyaXRpbmcsIHRydWUpO1xuICAgIH1cbn1cblxuLyoqXG4gKiBBbiBleGNlcHRpb24gdGhhdCBpcyB0aHJvd24gd2hlbiB0aGUgcHJvY2VzcyBleGl0cy5cbiAqKi9cbmV4cG9ydCBjbGFzcyBXQVNJUHJvY0V4aXQge1xuICAgIGNvbnN0cnVjdG9yKHB1YmxpYyByZWFkb25seSBjb2RlOiBudW1iZXIpIHsgfVxuXG4gICAgLyoqIEBkZXByZWNhdGVkIFVzZSAnY29kZScgaW5zdGVhZC5cbiAgICAgKiAgSGFzIGJlZW4gcmVuYW1lZCB0byBoYXZlIGxvb3NlIGNvbXBhdGliaWxpdHlcbiAgICAgKiAgd2l0aCBvdGhlciBpbXBsZW1lbnRhdGlvbnMgKiovXG4gICAgZ2V0IGV4aXRDb2RlKCkge1xuICAgICAgICByZXR1cm4gdGhpcy5jb2RlO1xuICAgIH1cbn0iLAogICAgImltcG9ydCB7IFdBU0lBYmkgfSBmcm9tIFwiLi4vYWJpXCI7XG5pbXBvcnQgdHlwZSB7IFdBU0lPcHRpb25zIH0gZnJvbSBcIi4uL29wdGlvbnNcIjtcblxuLyoqXG4gKiBBIGZlYXR1cmUgcHJvdmlkZXIgdGhhdCBwcm92aWRlcyBgYXJnc19nZXRgIGFuZCBgYXJnc19zaXplc19nZXRgXG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VBcmdzKG9wdGlvbnM6IFdBU0lPcHRpb25zLCBhYmk6IFdBU0lBYmksIG1lbW9yeVZpZXc6ICgpID0+IERhdGFWaWV3KTogV2ViQXNzZW1ibHkuTW9kdWxlSW1wb3J0cyB7XG4gICAgY29uc3QgYXJncyA9IG9wdGlvbnMuYXJncyB8fCBbXTtcbiAgICByZXR1cm4ge1xuICAgICAgICBhcmdzX2dldDogKGFyZ3Y6IG51bWJlciwgYXJndkJ1ZjogbnVtYmVyKSA9PiB7XG4gICAgICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuICAgICAgICAgICAgYWJpLndyaXRlU3RyaW5nQXJyYXkodmlldywgYXJncywgYXJndiwgYXJndkJ1Zik7XG4gICAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgICB9LFxuICAgICAgICBhcmdzX3NpemVzX2dldDogKGFyZ2M6IG51bWJlciwgYXJndkJ1ZlNpemU6IG51bWJlcikgPT4ge1xuICAgICAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgICAgIHZpZXcuc2V0VWludDMyKGFyZ2MsIGFyZ3MubGVuZ3RoLCB0cnVlKTtcbiAgICAgICAgICAgIGNvbnN0IHNpemVzID0gYWJpLnN0cmluZ0FycmF5U2l6ZShhcmdzKTtcbiAgICAgICAgICAgIHZpZXcuc2V0VWludDMyKGFyZ3ZCdWZTaXplLCBzaXplcy5idWZmZXJTaXplLCB0cnVlKTtcbiAgICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH0sXG4gICAgfTtcbn0iLAogICAgImltcG9ydCB7IFdBU0lBYmkgfSBmcm9tIFwiLi4vYWJpXCI7XG5pbXBvcnQgdHlwZSB7IFdBU0lPcHRpb25zIH0gZnJvbSBcIi4uL29wdGlvbnNcIjtcblxuLyoqXG4gKiBBIGZlYXR1cmUgcHJvdmlkZXIgdGhhdCBwcm92aWRlcyBgY2xvY2tfcmVzX2dldGAgYW5kIGBjbG9ja190aW1lX2dldGAgYnkgSmF2YVNjcmlwdCdzIERhdGUuXG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VDbG9jayhfb3B0aW9uczogV0FTSU9wdGlvbnMsIF9hYmk6IFdBU0lBYmksIG1lbW9yeVZpZXc6ICgpID0+IERhdGFWaWV3KTogV2ViQXNzZW1ibHkuTW9kdWxlSW1wb3J0cyB7XG4gICAgcmV0dXJuIHtcbiAgICAgICAgY2xvY2tfcmVzX2dldDogKGNsb2NrSWQ6IG51bWJlciwgcmVzb2x1dGlvbjogbnVtYmVyKSA9PiB7XG4gICAgICAgICAgICBsZXQgcmVzb2x1dGlvblZhbHVlOiBudW1iZXI7XG4gICAgICAgICAgICBzd2l0Y2ggKGNsb2NrSWQpIHtcbiAgICAgICAgICAgICAgICBjYXNlIFdBU0lBYmkuV0FTSV9DTE9DS19NT05PVE9OSUM6IHtcbiAgICAgICAgICAgICAgICAgICAgLy8gaHR0cHM6Ly9kZXZlbG9wZXIubW96aWxsYS5vcmcvZW4tVVMvZG9jcy9XZWIvQVBJL1BlcmZvcm1hbmNlL25vd1xuICAgICAgICAgICAgICAgICAgICByZXNvbHV0aW9uVmFsdWUgPSA1MDAwO1xuICAgICAgICAgICAgICAgICAgICBicmVhaztcbiAgICAgICAgICAgICAgICB9XG4gICAgICAgICAgICAgICAgY2FzZSBXQVNJQWJpLldBU0lfQ0xPQ0tfUkVBTFRJTUU6IHtcbiAgICAgICAgICAgICAgICAgICAgcmVzb2x1dGlvblZhbHVlID0gMTAwMDtcbiAgICAgICAgICAgICAgICAgICAgYnJlYWs7XG4gICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgICAgIGRlZmF1bHQ6IHJldHVybiBXQVNJQWJpLldBU0lfRU5PU1lTO1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgICAgIC8vIDY0LWJpdCBpbnRlZ2VyLCBidXQgb25seSB0aGUgbG93ZXIgMzIgYml0cyBhcmUgdXNlZC5cbiAgICAgICAgICAgIHZpZXcuc2V0VWludDMyKHJlc29sdXRpb24sIHJlc29sdXRpb25WYWx1ZSwgdHJ1ZSk7XG4gICAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgICB9LFxuICAgICAgICBjbG9ja190aW1lX2dldDogKGNsb2NrSWQ6IG51bWJlciwgX3ByZWNpc2lvbjogbnVtYmVyLCB0aW1lOiBudW1iZXIpID0+IHtcbiAgICAgICAgICAgIGxldCBub3dNcyA9IDA7XG4gICAgICAgICAgICBzd2l0Y2ggKGNsb2NrSWQpIHtcbiAgICAgICAgICAgICAgICBjYXNlIFdBU0lBYmkuV0FTSV9DTE9DS19NT05PVE9OSUM6IHtcbiAgICAgICAgICAgICAgICAgICAgbm93TXMgPSBwZXJmb3JtYW5jZS5ub3coKTtcbiAgICAgICAgICAgICAgICAgICAgYnJlYWs7XG4gICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgICAgIGNhc2UgV0FTSUFiaS5XQVNJX0NMT0NLX1JFQUxUSU1FOiB7XG4gICAgICAgICAgICAgICAgICAgIG5vd01zID0gRGF0ZS5ub3coKTtcbiAgICAgICAgICAgICAgICAgICAgYnJlYWs7XG4gICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgICAgIGRlZmF1bHQ6IHJldHVybiBXQVNJQWJpLldBU0lfRU5PU1lTO1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgICAgIGlmIChCaWdJbnQpIHtcbiAgICAgICAgICAgICAgICBjb25zdCBtc1RvTnMgPSAobXM6IG51bWJlcikgPT4ge1xuICAgICAgICAgICAgICAgICAgICBjb25zdCBtc0ludCA9IE1hdGgudHJ1bmMobXMpO1xuICAgICAgICAgICAgICAgICAgICBjb25zdCBkZWNpbWFsID0gQmlnSW50KE1hdGgucm91bmQoKG1zIC0gbXNJbnQpICogMV8wMDBfMDAwKSk7XG4gICAgICAgICAgICAgICAgICAgIGNvbnN0IG5zID0gQmlnSW50KG1zSW50KSAqIEJpZ0ludCgxXzAwMF8wMDApO1xuICAgICAgICAgICAgICAgICAgICByZXR1cm4gbnMgKyBkZWNpbWFsO1xuICAgICAgICAgICAgICAgIH07XG4gICAgICAgICAgICAgICAgY29uc3Qgbm93ID0gQmlnSW50KG1zVG9Ocyhub3dNcykpO1xuICAgICAgICAgICAgICAgIHZpZXcuc2V0QmlnVWludDY0KHRpbWUsIG5vdywgdHJ1ZSk7XG4gICAgICAgICAgICB9IGVsc2Uge1xuICAgICAgICAgICAgICAgIC8vIEZhbGxiYWNrIHRvIHR3byAzMi1iaXQgbnVtYmVycyBsb3NpbmcgcHJlY2lzaW9uXG4gICAgICAgICAgICAgICAgY29uc3Qgbm93ID0gRGF0ZS5ub3coKSAqIDFfMDAwXzAwMDtcbiAgICAgICAgICAgICAgICB2aWV3LnNldFVpbnQzMih0aW1lLCBub3cgJiAweDAwMDBmZmZmLCB0cnVlKTtcbiAgICAgICAgICAgICAgICB2aWV3LnNldFVpbnQzMih0aW1lICsgNCwgbm93ICYgMHhmZmZmMDAwMCwgdHJ1ZSk7XG4gICAgICAgICAgICB9XG4gICAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgICB9LFxuICAgIH07XG59IiwKICAgICJpbXBvcnQgeyBXQVNJQWJpIH0gZnJvbSBcIi4uL2FiaVwiO1xuaW1wb3J0IHR5cGUgeyBXQVNJT3B0aW9ucyB9IGZyb20gXCIuLi9vcHRpb25zXCI7XG5cbi8qKlxuICogQSBmZWF0dXJlIHByb3ZpZGVyIHRoYXQgcHJvdmlkZXMgYGVudmlyb25fZ2V0YCBhbmQgYGVudmlyb25fc2l6ZXNfZ2V0YFxuICovXG5leHBvcnQgZnVuY3Rpb24gdXNlRW52aXJvbihvcHRpb25zOiBXQVNJT3B0aW9ucywgYWJpOiBXQVNJQWJpLCBtZW1vcnlWaWV3OiAoKSA9PiBEYXRhVmlldyk6IFdlYkFzc2VtYmx5Lk1vZHVsZUltcG9ydHMge1xuICAgIHJldHVybiB7XG4gICAgICAgIGVudmlyb25fZ2V0OiAoZW52aXJvbjogbnVtYmVyLCBlbnZpcm9uQnVmOiBudW1iZXIpID0+IHtcbiAgICAgICAgICAgIGxldCBvZmZzZXRPZmZzZXQgPSBlbnZpcm9uO1xuICAgICAgICAgICAgbGV0IGJ1ZmZlck9mZnNldCA9IGVudmlyb25CdWY7XG4gICAgICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuICAgICAgICAgICAgZm9yIChjb25zdCBrZXkgaW4gb3B0aW9ucy5lbnYpIHtcbiAgICAgICAgICAgICAgICBjb25zdCB2YWx1ZSA9IG9wdGlvbnMuZW52W2tleV07XG4gICAgICAgICAgICAgICAgdmlldy5zZXRVaW50MzIob2Zmc2V0T2Zmc2V0LCBidWZmZXJPZmZzZXQsIHRydWUpO1xuICAgICAgICAgICAgICAgIG9mZnNldE9mZnNldCArPSA0O1xuICAgICAgICAgICAgICAgIGJ1ZmZlck9mZnNldCArPSBhYmkud3JpdGVTdHJpbmcodmlldywgYCR7a2V5fT0ke3ZhbHVlfVxcMGAsIGJ1ZmZlck9mZnNldCk7XG4gICAgICAgICAgICB9XG4gICAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgICB9LFxuICAgICAgICBlbnZpcm9uX3NpemVzX2dldDogKGVudmlyb246IG51bWJlciwgZW52aXJvbkJ1ZlNpemU6IG51bWJlcikgPT4ge1xuICAgICAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgICAgIHZpZXcuc2V0VWludDMyKGVudmlyb24sIE9iamVjdC5rZXlzKG9wdGlvbnMuZW52IHx8IHt9KS5sZW5ndGgsIHRydWUpO1xuICAgICAgICAgICAgdmlldy5zZXRVaW50MzIoXG4gICAgICAgICAgICAgICAgZW52aXJvbkJ1ZlNpemUsXG4gICAgICAgICAgICAgICAgT2JqZWN0LmVudHJpZXMob3B0aW9ucy5lbnYgfHwge30pLnJlZHVjZShcbiAgICAgICAgICAgICAgICAgICAgKGFjYywgW2tleSwgdmFsdWVdKSA9PiB7XG4gICAgICAgICAgICAgICAgICAgICAgICByZXR1cm4gYWNjICsgYWJpLmJ5dGVMZW5ndGgoa2V5KSAvKiA9ICovICsgMSArIGFiaS5ieXRlTGVuZ3RoKHZhbHVlKSAvKiBcXDAgKi8gKyAxO1xuICAgICAgICAgICAgICAgICAgICB9LFxuICAgICAgICAgICAgICAgICAgICAwXG4gICAgICAgICAgICAgICAgKSxcbiAgICAgICAgICAgICAgICB0cnVlXG4gICAgICAgICAgICApO1xuICAgICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgICAgfVxuICAgIH07XG59IiwKICAgICJpbXBvcnQgeyBXQVNJQWJpIH0gZnJvbSBcIi4uL2FiaVwiO1xuaW1wb3J0IHR5cGUgeyBXQVNJRmVhdHVyZVByb3ZpZGVyLCBXQVNJT3B0aW9ucyB9IGZyb20gXCIuLi9vcHRpb25zXCI7XG5cbmludGVyZmFjZSBGZEVudHJ5IHtcbiAgd3JpdGV2KGlvdnM6IFVpbnQ4QXJyYXlbXSk6IG51bWJlcjtcbiAgcmVhZHYoaW92czogVWludDhBcnJheVtdKTogbnVtYmVyO1xuICBjbG9zZSgpOiB2b2lkO1xufVxuXG5jbGFzcyBXcml0YWJsZVRleHRQcm94eSBpbXBsZW1lbnRzIEZkRW50cnkge1xuICBwcml2YXRlIGRlY29kZXIgPSBuZXcgVGV4dERlY29kZXIoXCJ1dGYtOFwiKTtcbiAgY29uc3RydWN0b3IoXG4gICAgcHJpdmF0ZSByZWFkb25seSBoYW5kbGVyOiAobGluZXM6IHN0cmluZyB8IFVpbnQ4QXJyYXkpID0+IHZvaWQsXG4gICAgcHJpdmF0ZSByZWFkb25seSBvdXRwdXRCdWZmZXJzOiBib29sZWFuXG4gICkgeyB9XG5cbiAgd3JpdGV2KGlvdnM6IFVpbnQ4QXJyYXlbXSk6IG51bWJlciB7XG4gICAgY29uc3QgdG90YWxCdWZmZXJTaXplID0gaW92cy5yZWR1Y2UoKGFjYywgaW92KSA9PiBhY2MgKyBpb3YuYnl0ZUxlbmd0aCwgMCk7XG4gICAgbGV0IG9mZnNldCA9IDA7XG4gICAgY29uc3QgY29uY2F0QnVmZmVyID0gbmV3IFVpbnQ4QXJyYXkodG90YWxCdWZmZXJTaXplKTtcbiAgICBmb3IgKGNvbnN0IGJ1ZmZlciBvZiBpb3ZzKSB7XG4gICAgICBjb25jYXRCdWZmZXIuc2V0KGJ1ZmZlciwgb2Zmc2V0KTtcbiAgICAgIG9mZnNldCArPSBidWZmZXIuYnl0ZUxlbmd0aDtcbiAgICB9XG5cbiAgICBpZiAodGhpcy5vdXRwdXRCdWZmZXJzKSB7XG4gICAgICB0aGlzLmhhbmRsZXIoY29uY2F0QnVmZmVyKTtcbiAgICB9IGVsc2Uge1xuICAgICAgY29uc3QgbGluZXMgPSB0aGlzLmRlY29kZXIuZGVjb2RlKGNvbmNhdEJ1ZmZlcik7XG4gICAgICB0aGlzLmhhbmRsZXIobGluZXMpO1xuICAgIH1cblxuICAgIHJldHVybiBjb25jYXRCdWZmZXIubGVuZ3RoO1xuICB9XG4gIHJlYWR2KF9pb3ZzOiBVaW50OEFycmF5W10pOiBudW1iZXIge1xuICAgIHJldHVybiAwO1xuICB9XG4gIGNsb3NlKCk6IHZvaWQgeyB9XG59XG5cbmV4cG9ydCBjbGFzcyBSZWFkYWJsZVRleHRQcm94eSBpbXBsZW1lbnRzIEZkRW50cnkge1xuICBwcml2YXRlIGVuY29kZXIgPSBuZXcgVGV4dEVuY29kZXIoKTtcbiAgcHJpdmF0ZSBwZW5kaW5nOiBVaW50OEFycmF5IHwgbnVsbCA9IG51bGw7XG4gIGNvbnN0cnVjdG9yKHByaXZhdGUgcmVhZG9ubHkgY29uc3VtZTogKCkgPT4gc3RyaW5nIHwgVWludDhBcnJheSkgeyB9XG5cbiAgd3JpdGV2KF9pb3ZzOiBVaW50OEFycmF5W10pOiBudW1iZXIge1xuICAgIHJldHVybiAwO1xuICB9XG4gIGNvbnN1bWVQZW5kaW5nKHBlbmRpbmc6IFVpbnQ4QXJyYXksIHJlcXVlc3RMZW5ndGg6IG51bWJlcik6IFVpbnQ4QXJyYXkge1xuICAgIGlmIChwZW5kaW5nLmJ5dGVMZW5ndGggPCByZXF1ZXN0TGVuZ3RoKSB7XG4gICAgICB0aGlzLnBlbmRpbmcgPSBudWxsO1xuICAgICAgcmV0dXJuIHBlbmRpbmc7XG4gICAgfVxuICAgIGNvbnN0IHJlc3VsdCA9IHBlbmRpbmcuc2xpY2UoMCwgcmVxdWVzdExlbmd0aCk7XG4gICAgdGhpcy5wZW5kaW5nID0gcGVuZGluZy5zbGljZShyZXF1ZXN0TGVuZ3RoKTtcbiAgICByZXR1cm4gcmVzdWx0O1xuICB9XG4gIHJlYWR2KGlvdnM6IFVpbnQ4QXJyYXlbXSk6IG51bWJlciB7XG4gICAgbGV0IHJlYWQgPSAwO1xuICAgIGZvciAoY29uc3QgYnVmZmVyIG9mIGlvdnMpIHtcbiAgICAgIGxldCByZW1haW5pbmcgPSBidWZmZXIuYnl0ZUxlbmd0aDtcbiAgICAgIGlmICh0aGlzLnBlbmRpbmcpIHtcbiAgICAgICAgY29uc3QgY29uc3VtZWQgPSB0aGlzLmNvbnN1bWVQZW5kaW5nKHRoaXMucGVuZGluZywgcmVtYWluaW5nKTtcbiAgICAgICAgYnVmZmVyLnNldChjb25zdW1lZCwgMCk7XG4gICAgICAgIHJlbWFpbmluZyAtPSBjb25zdW1lZC5ieXRlTGVuZ3RoO1xuICAgICAgICByZWFkICs9IGNvbnN1bWVkLmJ5dGVMZW5ndGg7XG4gICAgICB9XG4gICAgICB3aGlsZSAocmVtYWluaW5nID4gMCkge1xuICAgICAgICBjb25zdCBuZXdEYXRhID0gdGhpcy5jb25zdW1lKCk7XG4gICAgICAgIGxldCBieXRlczogVWludDhBcnJheTtcblxuICAgICAgICBpZiAobmV3RGF0YSBpbnN0YW5jZW9mIFVpbnQ4QXJyYXkpIHtcbiAgICAgICAgICBieXRlcyA9IG5ld0RhdGE7XG4gICAgICAgIH0gZWxzZSB7XG4gICAgICAgICAgYnl0ZXMgPSB0aGlzLmVuY29kZXIuZW5jb2RlKG5ld0RhdGEpO1xuICAgICAgICB9XG5cbiAgICAgICAgaWYgKGJ5dGVzLmxlbmd0aCA9PT0gMCkge1xuICAgICAgICAgIHJldHVybiByZWFkO1xuICAgICAgICB9XG4gICAgICAgIGlmIChieXRlcy5sZW5ndGggPiByZW1haW5pbmcpIHtcbiAgICAgICAgICBidWZmZXIuc2V0KGJ5dGVzLnNsaWNlKDAsIHJlbWFpbmluZyksIGJ1ZmZlci5ieXRlTGVuZ3RoIC0gcmVtYWluaW5nKTtcbiAgICAgICAgICB0aGlzLnBlbmRpbmcgPSBieXRlcy5zbGljZShyZW1haW5pbmcpO1xuICAgICAgICAgIHJlYWQgKz0gcmVtYWluaW5nO1xuICAgICAgICAgIHJlbWFpbmluZyA9IDA7XG4gICAgICAgIH0gZWxzZSB7XG4gICAgICAgICAgYnVmZmVyLnNldChieXRlcywgYnVmZmVyLmJ5dGVMZW5ndGggLSByZW1haW5pbmcpO1xuICAgICAgICAgIHJlYWQgKz0gYnl0ZXMubGVuZ3RoO1xuICAgICAgICAgIHJlbWFpbmluZyAtPSBieXRlcy5sZW5ndGg7XG4gICAgICAgIH1cbiAgICAgIH1cbiAgICB9XG4gICAgcmV0dXJuIHJlYWQ7XG4gIH1cbiAgY2xvc2UoKTogdm9pZCB7IH1cbn1cblxuZXhwb3J0IHR5cGUgU3RkSW9PcHRpb25zID0ge1xuICBzdGRpbj86ICgpID0+IHN0cmluZyB8IFVpbnQ4QXJyYXk7XG4gIHN0ZG91dD86IChsaW5lczogc3RyaW5nIHwgVWludDhBcnJheSkgPT4gdm9pZDtcbiAgc3RkZXJyPzogKGxpbmVzOiBzdHJpbmcgfCBVaW50OEFycmF5KSA9PiB2b2lkO1xuICBvdXRwdXRCdWZmZXJzPzogYm9vbGVhbjtcbn07XG5cbmZ1bmN0aW9uIGJpbmRTdGRpbyhcbiAgdXNlT3B0aW9uczogU3RkSW9PcHRpb25zID0ge31cbik6IChSZWFkYWJsZVRleHRQcm94eSB8IFdyaXRhYmxlVGV4dFByb3h5KVtdIHtcbiAgY29uc3Qgb3V0cHV0QnVmZmVycyA9IHVzZU9wdGlvbnMub3V0cHV0QnVmZmVycyB8fCBmYWxzZTtcbiAgcmV0dXJuIFtcbiAgICBuZXcgUmVhZGFibGVUZXh0UHJveHkoXG4gICAgICB1c2VPcHRpb25zLnN0ZGluIHx8XG4gICAgICAoKCkgPT4ge1xuICAgICAgICByZXR1cm4gXCJcIjtcbiAgICAgIH0pXG4gICAgKSxcbiAgICBuZXcgV3JpdGFibGVUZXh0UHJveHkodXNlT3B0aW9ucy5zdGRvdXQgfHwgY29uc29sZS5sb2csIG91dHB1dEJ1ZmZlcnMpLFxuICAgIG5ldyBXcml0YWJsZVRleHRQcm94eSh1c2VPcHRpb25zLnN0ZGVyciB8fCBjb25zb2xlLmVycm9yLCBvdXRwdXRCdWZmZXJzKSxcbiAgXTtcbn1cblxuLyoqXG4gKiBDcmVhdGUgYSBmZWF0dXJlIHByb3ZpZGVyIHRoYXQgcHJvdmlkZXMgZmQgcmVsYXRlZCBmZWF0dXJlcyBvbmx5IGZvciBzdGFuZGFyZCBvdXRwdXQgYW5kIHN0YW5kYXJkIGVycm9yXG4gKiBJdCB1c2VzIEphdmFTY3JpcHQncyBgY29uc29sZWAgQVBJcyBhcyBiYWNrZW5kIGJ5IGRlZmF1bHQuXG4gKlxuICogYGBganNcbiAqIGNvbnN0IHdhc2kgPSBuZXcgV0FTSSh7XG4gKiAgIGZlYXR1cmVzOiBbdXNlU3RkaW8oKV0sXG4gKiB9KTtcbiAqIGBgYFxuICpcbiAqIFRvIHVzZSBhIGN1c3RvbSBiYWNrZW5kLCB5b3UgY2FuIHBhc3Mgc3Rkb3V0IGFuZCBzdGRlcnIgaGFuZGxlcnMuXG4gKlxuICogYGBganNcbiAqIGNvbnN0IHdhc2kgPSBuZXcgV0FTSSh7XG4gKiAgIGZlYXR1cmVzOiBbXG4gKiAgICAgdXNlU3RkaW8oe1xuICogICAgICAgc3Rkb3V0OiAobGluZXMpID0+IGRvY3VtZW50LndyaXRlKGxpbmVzKSxcbiAqICAgICAgIHN0ZGVycjogKGxpbmVzKSA9PiBkb2N1bWVudC53cml0ZShsaW5lcyksXG4gKiAgICAgfSlcbiAqICAgXSxcbiAqIH0pO1xuICogYGBgXG4gKlxuICogVGhpcyBwcm92aWRlcyBgZmRfd3JpdGVgLCBgZmRfcHJlc3RhdF9nZXRgIGFuZCBgZmRfcHJlc3RhdF9kaXJfbmFtZWAgaW1wbGVtZW50YXRpb25zIHRvIG1ha2UgbGliYyB3b3JrIHdpdGggbWluaW1hbCBlZmZvcnQuXG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VTdGRpbyh1c2VPcHRpb25zOiBTdGRJb09wdGlvbnMgPSB7fSk6IFdBU0lGZWF0dXJlUHJvdmlkZXIge1xuICByZXR1cm4gKF9vcHRpb25zLCBhYmksIG1lbW9yeVZpZXcpID0+IHtcbiAgICBjb25zdCBmZFRhYmxlID0gYmluZFN0ZGlvKHVzZU9wdGlvbnMpO1xuICAgIHJldHVybiB7XG4gICAgICBmZF9mZHN0YXRfZ2V0OiAoZmQ6IG51bWJlciwgYnVmOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgZmRFbnRyeSA9IGZkVGFibGVbZmRdO1xuICAgICAgICBpZiAoIWZkRW50cnkpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgY29uc3QgYWxsUmlnaHRzID0gMHgxZmZmZmZmZm47XG4gICAgICAgIGFiaS53cml0ZUZkc3RhdCh2aWV3LCBidWYsIFdBU0lBYmkuV0FTSV9GSUxFVFlQRV9DSEFSQUNURVJfREVWSUNFLCAwLCBhbGxSaWdodHMsIGFsbFJpZ2h0cyk7XG4gICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICB9LFxuICAgICAgZmRfZmlsZXN0YXRfZ2V0OiAoZmQ6IG51bWJlciwgYnVmOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgZmRFbnRyeSA9IGZkVGFibGVbZmRdO1xuICAgICAgICBpZiAoIWZkRW50cnkpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgYWJpLndyaXRlRmlsZXN0YXQodmlldywgYnVmLCBXQVNJQWJpLldBU0lfRklMRVRZUEVfQ0hBUkFDVEVSX0RFVklDRSk7XG4gICAgICB9LFxuICAgICAgZmRfcHJlc3RhdF9nZXQ6IChfZmQ6IG51bWJlciwgX2J1ZjogbnVtYmVyKSA9PiB7XG4gICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcbiAgICAgIH0sXG4gICAgICBmZF9wcmVzdGF0X2Rpcl9uYW1lOiAoX2ZkOiBudW1iZXIsIF9idWY6IG51bWJlcikgPT4ge1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG4gICAgICB9LFxuICAgICAgZmRfd3JpdGU6IChcbiAgICAgICAgZmQ6IG51bWJlcixcbiAgICAgICAgaW92czogbnVtYmVyLFxuICAgICAgICBpb3ZzTGVuOiBudW1iZXIsXG4gICAgICAgIG53cml0dGVuOiBudW1iZXJcbiAgICAgICkgPT4ge1xuICAgICAgICBjb25zdCBmZEVudHJ5ID0gZmRUYWJsZVtmZF07XG4gICAgICAgIGlmICghZmRFbnRyeSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuICAgICAgICBjb25zdCBpb3ZzQnVmZmVycyA9IGFiaS5pb3ZWaWV3cyh2aWV3LCBpb3ZzLCBpb3ZzTGVuKTtcbiAgICAgICAgY29uc3Qgd3JpdHRlblZhbHVlID0gZmRFbnRyeS53cml0ZXYoaW92c0J1ZmZlcnMpO1xuICAgICAgICB2aWV3LnNldFVpbnQzMihud3JpdHRlbiwgd3JpdHRlblZhbHVlLCB0cnVlKTtcbiAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgIH0sXG4gICAgICBmZF9yZWFkOiAoZmQ6IG51bWJlciwgaW92czogbnVtYmVyLCBpb3ZzTGVuOiBudW1iZXIsIG5yZWFkOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgZmRFbnRyeSA9IGZkVGFibGVbZmRdO1xuICAgICAgICBpZiAoIWZkRW50cnkpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgY29uc3QgaW92c0J1ZmZlcnMgPSBhYmkuaW92Vmlld3ModmlldywgaW92cywgaW92c0xlbik7XG4gICAgICAgIGNvbnN0IHJlYWRWYWx1ZSA9IGZkRW50cnkucmVhZHYoaW92c0J1ZmZlcnMpO1xuICAgICAgICB2aWV3LnNldFVpbnQzMihucmVhZCwgcmVhZFZhbHVlLCB0cnVlKTtcbiAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgIH0sXG4gICAgfTtcbiAgfTtcbn1cblxudHlwZSBGaWxlRGVzY3JpcHRvciA9IG51bWJlcjtcblxuLyoqXG4gKiBSZXByZXNlbnRzIGEgbm9kZSBpbiB0aGUgZmlsZSBzeXN0ZW0gdGhhdCBpcyBhIGRpcmVjdG9yeS5cbiAqL1xuaW50ZXJmYWNlIERpcmVjdG9yeU5vZGUge1xuICByZWFkb25seSB0eXBlOiBcImRpclwiO1xuICBlbnRyaWVzOiBSZWNvcmQ8c3RyaW5nLCBGU05vZGU+O1xufVxuXG4vKipcbiAqIFJlcHJlc2VudHMgYSBub2RlIGluIHRoZSBmaWxlIHN5c3RlbSB0aGF0IGlzIGEgZmlsZS5cbiAqL1xuaW50ZXJmYWNlIEZpbGVOb2RlIHtcbiAgcmVhZG9ubHkgdHlwZTogXCJmaWxlXCI7XG4gIGNvbnRlbnQ6IFVpbnQ4QXJyYXkgfCBCbG9iO1xufVxuXG50eXBlIENoYXJhY3RlckRldmljZU5vZGUgPVxuICB8IHsgcmVhZG9ubHkgdHlwZTogXCJjaGFyYWN0ZXJcIjsga2luZDogXCJzdGRpb1wiOyBlbnRyeTogRmRFbnRyeSB9XG4gIHwgeyByZWFkb25seSB0eXBlOiBcImNoYXJhY3RlclwiOyBraW5kOiBcImRldm51bGxcIiB9O1xuXG4vKipcbiAqIFVuaW9uIHR5cGUgcmVwcmVzZW50aW5nIGFueSBub2RlIGluIHRoZSBmaWxlIHN5c3RlbS5cbiAqL1xudHlwZSBGU05vZGUgPSBEaXJlY3RvcnlOb2RlIHwgRmlsZU5vZGUgfCBDaGFyYWN0ZXJEZXZpY2VOb2RlO1xuXG4vKipcbiAqIFJlcHJlc2VudHMgYW4gb3BlbiBmaWxlIGluIHRoZSBmaWxlIHN5c3RlbS5cbiAqL1xuaW50ZXJmYWNlIE9wZW5GaWxlIHtcbiAgbm9kZTogRlNOb2RlO1xuICBwb3NpdGlvbjogbnVtYmVyO1xuICBwYXRoOiBzdHJpbmc7XG4gIGlzUHJlb3Blbj86IGJvb2xlYW47XG4gIHByZW9wZW5QYXRoPzogc3RyaW5nO1xuICBmZDogRmlsZURlc2NyaXB0b3I7XG59XG5cbi8qKlxuICogVHlwZSBmb3IgZmlsZSBjb250ZW50IHRoYXQgY2FuIGJlIGFkZGVkIHRvIHRoZSBmaWxlIHN5c3RlbS5cbiAqL1xudHlwZSBGaWxlQ29udGVudCA9IHN0cmluZyB8IFVpbnQ4QXJyYXkgfCBCbG9iO1xuXG4vKipcbiAqIEluLW1lbW9yeSBpbXBsZW1lbnRhdGlvbiBvZiBhIGZpbGUgc3lzdGVtLlxuICovXG5leHBvcnQgY2xhc3MgTWVtb3J5RmlsZVN5c3RlbSB7XG4gIHByaXZhdGUgcm9vdDogRGlyZWN0b3J5Tm9kZTtcbiAgcHJpdmF0ZSBwcmVvcGVuUGF0aHM6IHN0cmluZ1tdID0gW107XG5cbiAgLyoqXG4gICAqIENyZWF0ZXMgYSBuZXcgbWVtb3J5IGZpbGUgc3lzdGVtLlxuICAgKiBAcGFyYW0gcHJlb3BlbnMgT3B0aW9uYWwgbGlzdCBvZiBkaXJlY3RvcmllcyB0byBwcmUtb3BlblxuICAgKi9cbiAgY29uc3RydWN0b3IocHJlb3BlbnM/OiB7IFtndWVzdFBhdGg6IHN0cmluZ106IHN0cmluZyB9IHwgdW5kZWZpbmVkKSB7XG4gICAgdGhpcy5yb290ID0geyB0eXBlOiBcImRpclwiLCBlbnRyaWVzOiB7fSB9O1xuXG4gICAgLy8gU2V0dXAgZXNzZW50aWFsIGRpcmVjdG9yaWVzIGFuZCBzcGVjaWFsIGZpbGVzXG4gICAgdGhpcy5lbnN1cmVEaXIoXCIvZGV2XCIpO1xuICAgIHRoaXMuc2V0Tm9kZShcIi9kZXYvbnVsbFwiLCB7IHR5cGU6IFwiY2hhcmFjdGVyXCIsIGtpbmQ6IFwiZGV2bnVsbFwiIH0pO1xuXG4gICAgLy8gU2V0dXAgcHJlb3BlbmVkIGRpcmVjdG9yaWVzXG4gICAgaWYgKHByZW9wZW5zKSB7XG4gICAgICBmb3IgKGNvbnN0IGd1ZXN0UGF0aCBvZiBPYmplY3Qua2V5cyhwcmVvcGVucykpIHtcbiAgICAgICAgLy8gdGhlcmUgYXJlIG5vICdob3N0JyBwYXRocyBpbiBhIG1lbW9yeSBmaWxlIHN5c3RlbSwgc28gd2UganVzdCB1c2UgdGhlIGd1ZXN0IHBhdGguXG4gICAgICAgIHRoaXMuZW5zdXJlRGlyKGd1ZXN0UGF0aCk7XG4gICAgICAgIHRoaXMucHJlb3BlblBhdGhzLnB1c2goZ3Vlc3RQYXRoKTtcbiAgICAgIH1cbiAgICB9IGVsc2Uge1xuICAgICAgdGhpcy5wcmVvcGVuUGF0aHMucHVzaChcIi9cIik7XG4gICAgfVxuICB9XG5cbiAgcmVtb3ZlRmlsZShwYXRoOiBzdHJpbmcpIHtcbiAgICBjb25zdCBub3JtYWxpemVkUGF0aCA9IHRoaXMubm9ybWFsaXplUGF0aChwYXRoKTtcbiAgICBjb25zdCBwYXJ0cyA9IG5vcm1hbGl6ZWRQYXRoLnNwbGl0KFwiL1wiKS5maWx0ZXIoKHApID0+IHAubGVuZ3RoID4gMCk7XG4gICAgY29uc3QgZmlsZU5hbWUgPSBwYXJ0cy5wb3AoKTtcbiAgICBjb25zdCBkaXJQYXRoID0gYC8ke3BhcnRzLmpvaW4oXCIvXCIpfWA7XG4gICAgY29uc3QgZGlyID0gdGhpcy5lbnN1cmVEaXIoZGlyUGF0aCk7XG4gICAgaWYgKGZpbGVOYW1lKSB7XG4gICAgICBkZWxldGUgZGlyLmVudHJpZXNbZmlsZU5hbWVdO1xuICAgIH1cbiAgfVxuXG4gIGFkZEZpbGUocGF0aDogc3RyaW5nLCBjb250ZW50OiBGaWxlQ29udGVudCk6IHZvaWQge1xuICAgIGlmICh0eXBlb2YgY29udGVudCA9PT0gXCJzdHJpbmdcIikge1xuICAgICAgY29uc3QgZGF0YSA9IG5ldyBUZXh0RW5jb2RlcigpLmVuY29kZShjb250ZW50KTtcbiAgICAgIHRoaXMuY3JlYXRlRmlsZShwYXRoLCBkYXRhKTtcbiAgICAgIHJldHVybjtcbiAgICB9XG4gICAgdGhpcy5jcmVhdGVGaWxlKHBhdGgsIGNvbnRlbnQpO1xuICB9XG5cbiAgLyoqXG4gICAqIENyZWF0ZXMgYSBmaWxlIHdpdGggdGhlIHNwZWNpZmllZCBjb250ZW50LlxuICAgKiBAcGFyYW0gcGF0aCBQYXRoIHdoZXJlIHRoZSBmaWxlIHNob3VsZCBiZSBjcmVhdGVkXG4gICAqIEBwYXJhbSBjb250ZW50IEJpbmFyeSBjb250ZW50IG9mIHRoZSBmaWxlXG4gICAqIEByZXR1cm5zIFRoZSBjcmVhdGVkIGZpbGUgbm9kZVxuICAgKi9cbiAgY3JlYXRlRmlsZShwYXRoOiBzdHJpbmcsIGNvbnRlbnQ6IFVpbnQ4QXJyYXkgfCBCbG9iKTogRmlsZU5vZGUge1xuICAgIGNvbnN0IGZpbGVOb2RlOiBGaWxlTm9kZSA9IHsgdHlwZTogXCJmaWxlXCIsIGNvbnRlbnQgfTtcbiAgICB0aGlzLnNldE5vZGUocGF0aCwgZmlsZU5vZGUpO1xuICAgIHJldHVybiBmaWxlTm9kZTtcbiAgfVxuXG4gIC8qKlxuICAgKiBTZXRzIGEgbm9kZSBhdCB0aGUgc3BlY2lmaWVkIHBhdGguXG4gICAqIEBwYXJhbSBwYXRoIFBhdGggd2hlcmUgdGhlIG5vZGUgc2hvdWxkIGJlIHNldFxuICAgKiBAcGFyYW0gbm9kZSBUaGUgbm9kZSB0byBzZXRcbiAgICovXG4gIHNldE5vZGUocGF0aDogc3RyaW5nLCBub2RlOiBGU05vZGUpOiB2b2lkIHtcbiAgICBjb25zdCBub3JtYWxpemVkUGF0aCA9IHRoaXMubm9ybWFsaXplUGF0aChwYXRoKTtcbiAgICBjb25zdCBwYXJ0cyA9IG5vcm1hbGl6ZWRQYXRoLnNwbGl0KFwiL1wiKS5maWx0ZXIoKHApID0+IHAubGVuZ3RoID4gMCk7XG5cbiAgICBpZiAocGFydHMubGVuZ3RoID09PSAwKSB7XG4gICAgICBpZiAobm9kZS50eXBlICE9PSBcImRpclwiKSB7XG4gICAgICAgIHRocm93IG5ldyBFcnJvcihcIlJvb3QgbXVzdCBiZSBhIGRpcmVjdG9yeVwiKTtcbiAgICAgIH1cbiAgICAgIHRoaXMucm9vdCA9IG5vZGU7XG4gICAgICByZXR1cm47XG4gICAgfVxuXG4gICAgY29uc3QgZmlsZU5hbWUgPSBwYXJ0cy5wb3AoKTtcbiAgICBjb25zdCBkaXJQYXRoID0gYC8ke3BhcnRzLmpvaW4oXCIvXCIpfWA7XG4gICAgY29uc3QgZGlyID0gdGhpcy5lbnN1cmVEaXIoZGlyUGF0aCk7XG4gICAgaWYgKGZpbGVOYW1lKSB7XG4gICAgICBkaXIuZW50cmllc1tmaWxlTmFtZV0gPSBub2RlO1xuICAgIH1cbiAgfVxuXG4gIC8qKlxuICAgKiBHZXRzIHRoZSAvZGV2L251bGwgc3BlY2lhbCBkZXZpY2UuXG4gICAqIEByZXR1cm5zIFRoZSAvZGV2L251bGwgbm9kZVxuICAgKi9cbiAgZ2V0RGV2TnVsbCgpOiBGU05vZGUge1xuICAgIGNvbnN0IG5vZGUgPSB0aGlzLmxvb2t1cChcIi9kZXYvbnVsbFwiKTtcbiAgICBpZiAoIW5vZGUpIHRocm93IG5ldyBFcnJvcihcIi9kZXYvbnVsbCBub3QgZm91bmRcIik7XG4gICAgcmV0dXJuIG5vZGU7XG4gIH1cblxuICAvKipcbiAgICogR2V0cyB0aGUgbGlzdCBvZiBwcmUtb3BlbmVkIHBhdGhzLlxuICAgKiBAcmV0dXJucyBBcnJheSBvZiBwcmUtb3BlbmVkIHBhdGhzXG4gICAqL1xuICBnZXRQcmVvcGVuUGF0aHMoKTogc3RyaW5nW10ge1xuICAgIHJldHVybiBbLi4udGhpcy5wcmVvcGVuUGF0aHNdO1xuICB9XG5cbiAgLyoqXG4gICAqIExvb2tzIHVwIGEgbm9kZSBhdCB0aGUgc3BlY2lmaWVkIHBhdGguXG4gICAqIEBwYXJhbSBwYXRoIFBhdGggdG8gbG9vayB1cFxuICAgKiBAcmV0dXJucyBUaGUgbm9kZSBhdCB0aGUgcGF0aCwgb3IgbnVsbCBpZiBub3QgZm91bmRcbiAgICovXG4gIGxvb2t1cChwYXRoOiBzdHJpbmcpOiBGU05vZGUgfCBudWxsIHtcbiAgICBjb25zdCBub3JtYWxpemVkUGF0aCA9IHRoaXMubm9ybWFsaXplUGF0aChwYXRoKTtcbiAgICBpZiAobm9ybWFsaXplZFBhdGggPT09IFwiL1wiKSByZXR1cm4gdGhpcy5yb290O1xuXG4gICAgY29uc3QgcGFydHMgPSBub3JtYWxpemVkUGF0aC5zcGxpdChcIi9cIikuZmlsdGVyKChwKSA9PiBwLmxlbmd0aCA+IDApO1xuICAgIGxldCBjdXJyZW50OiBGU05vZGUgfCB1bmRlZmluZWQgPSB0aGlzLnJvb3Q7XG5cbiAgICBmb3IgKGNvbnN0IHBhcnQgb2YgcGFydHMpIHtcbiAgICAgIGlmIChjdXJyZW50LnR5cGUgIT09IFwiZGlyXCIpIHJldHVybiBudWxsO1xuICAgICAgY3VycmVudCA9IGN1cnJlbnQuZW50cmllc1twYXJ0XTtcbiAgICAgIGlmICghY3VycmVudCkgcmV0dXJuIG51bGw7XG4gICAgfVxuXG4gICAgcmV0dXJuIGN1cnJlbnQ7XG4gIH1cblxuICAvKipcbiAgICogUmVzb2x2ZXMgYSByZWxhdGl2ZSBwYXRoIGZyb20gYSBkaXJlY3RvcnkuXG4gICAqIEBwYXJhbSBkaXIgU3RhcnRpbmcgZGlyZWN0b3J5XG4gICAqIEBwYXJhbSByZWxhdGl2ZVBhdGggUmVsYXRpdmUgcGF0aCB0byByZXNvbHZlXG4gICAqIEByZXR1cm5zIFRoZSByZXNvbHZlZCBub2RlLCBvciBudWxsIGlmIG5vdCBmb3VuZFxuICAgKi9cbiAgcmVzb2x2ZShkaXI6IERpcmVjdG9yeU5vZGUsIHJlbGF0aXZlUGF0aDogc3RyaW5nKTogRlNOb2RlIHwgbnVsbCB7XG4gICAgY29uc3Qgbm9ybWFsaXplZFBhdGggPSB0aGlzLm5vcm1hbGl6ZVBhdGgocmVsYXRpdmVQYXRoKTtcbiAgICBjb25zdCBwYXJ0cyA9IG5vcm1hbGl6ZWRQYXRoLnNwbGl0KFwiL1wiKS5maWx0ZXIoKHApID0+IHAubGVuZ3RoID4gMCk7XG4gICAgbGV0IGN1cnJlbnQ6IEZTTm9kZSB8IHVuZGVmaW5lZCA9IGRpcjtcblxuICAgIGZvciAoY29uc3QgcGFydCBvZiBwYXJ0cykge1xuICAgICAgaWYgKHBhcnQgPT09IFwiLlwiKSBjb250aW51ZTtcbiAgICAgIGlmIChwYXJ0ID09PSBcIi4uXCIpIHtcbiAgICAgICAgY3VycmVudCA9IHRoaXMucm9vdDsgLy8ganVtcCB0byByb290XG4gICAgICAgIGNvbnRpbnVlO1xuICAgICAgfVxuICAgICAgaWYgKGN1cnJlbnQudHlwZSAhPT0gXCJkaXJcIikgcmV0dXJuIG51bGw7XG4gICAgICBjdXJyZW50ID0gY3VycmVudC5lbnRyaWVzW3BhcnRdO1xuICAgICAgaWYgKCFjdXJyZW50KSByZXR1cm4gbnVsbDtcbiAgICB9XG5cbiAgICByZXR1cm4gY3VycmVudDtcbiAgfVxuXG4gIC8qKlxuICAgKiBFbnN1cmVzIGEgZGlyZWN0b3J5IGV4aXN0cyBhdCB0aGUgc3BlY2lmaWVkIHBhdGgsIGNyZWF0aW5nIGl0IGlmIG5lY2Vzc2FyeS5cbiAgICogQHBhcmFtIHBhdGggUGF0aCB0byB0aGUgZGlyZWN0b3J5XG4gICAqIEByZXR1cm5zIFRoZSBkaXJlY3Rvcnkgbm9kZVxuICAgKi9cbiAgZW5zdXJlRGlyKHBhdGg6IHN0cmluZyk6IERpcmVjdG9yeU5vZGUge1xuICAgIGNvbnN0IG5vcm1hbGl6ZWRQYXRoID0gdGhpcy5ub3JtYWxpemVQYXRoKHBhdGgpO1xuICAgIGNvbnN0IHBhcnRzID0gbm9ybWFsaXplZFBhdGguc3BsaXQoXCIvXCIpLmZpbHRlcigocCkgPT4gcC5sZW5ndGggPiAwKTtcbiAgICBsZXQgY3VycmVudDogRGlyZWN0b3J5Tm9kZSA9IHRoaXMucm9vdDtcblxuICAgIGZvciAoY29uc3QgcGFydCBvZiBwYXJ0cykge1xuICAgICAgaWYgKCFjdXJyZW50LmVudHJpZXNbcGFydF0pIHtcbiAgICAgICAgY3VycmVudC5lbnRyaWVzW3BhcnRdID0geyB0eXBlOiBcImRpclwiLCBlbnRyaWVzOiB7fSB9O1xuICAgICAgfVxuXG4gICAgICBjb25zdCBuZXh0ID0gY3VycmVudC5lbnRyaWVzW3BhcnRdO1xuICAgICAgaWYgKG5leHQudHlwZSAhPT0gXCJkaXJcIikge1xuICAgICAgICB0aHJvdyBuZXcgRXJyb3IoYFwiJHtwYXJ0fVwiIGlzIG5vdCBhIGRpcmVjdG9yeWApO1xuICAgICAgfVxuXG4gICAgICBjdXJyZW50ID0gbmV4dDtcbiAgICB9XG5cbiAgICByZXR1cm4gY3VycmVudDtcbiAgfVxuXG4gIC8qKlxuICAgKiBDcmVhdGVzIGEgZmlsZSBpbiBhIGRpcmVjdG9yeS5cbiAgICogQHBhcmFtIGRpciBQYXJlbnQgZGlyZWN0b3J5XG4gICAqIEBwYXJhbSByZWxhdGl2ZVBhdGggUGF0aCByZWxhdGl2ZSB0byB0aGUgZGlyZWN0b3J5XG4gICAqIEByZXR1cm5zIFRoZSBjcmVhdGVkIGZpbGUgbm9kZVxuICAgKi9cbiAgY3JlYXRlRmlsZUluKGRpcjogRGlyZWN0b3J5Tm9kZSwgcmVsYXRpdmVQYXRoOiBzdHJpbmcpOiBGaWxlTm9kZSB7XG4gICAgY29uc3Qgbm9ybWFsaXplZFBhdGggPSB0aGlzLm5vcm1hbGl6ZVBhdGgocmVsYXRpdmVQYXRoKTtcbiAgICBjb25zdCBwYXJ0cyA9IG5vcm1hbGl6ZWRQYXRoLnNwbGl0KFwiL1wiKS5maWx0ZXIoKHApID0+IHAubGVuZ3RoID4gMCk7XG5cbiAgICBpZiAocGFydHMubGVuZ3RoID09PSAwKSB7XG4gICAgICB0aHJvdyBuZXcgRXJyb3IoXCJDYW5ub3QgY3JlYXRlIGEgZmlsZSB3aXRoIGFuIGVtcHR5IG5hbWVcIik7XG4gICAgfVxuXG4gICAgY29uc3QgZmlsZU5hbWUgPSBwYXJ0cy5wb3AoKTtcbiAgICBpZiAoIWZpbGVOYW1lKSB7XG4gICAgICB0aHJvdyBuZXcgRXJyb3IoXCJDYW5ub3QgY3JlYXRlIGEgZmlsZSB3aXRoIGFuIGVtcHR5IG5hbWVcIik7XG4gICAgfVxuICAgIGxldCBjdXJyZW50ID0gZGlyO1xuXG4gICAgZm9yIChjb25zdCBwYXJ0IG9mIHBhcnRzKSB7XG4gICAgICBpZiAoIWN1cnJlbnQuZW50cmllc1twYXJ0XSkge1xuICAgICAgICBjdXJyZW50LmVudHJpZXNbcGFydF0gPSB7IHR5cGU6IFwiZGlyXCIsIGVudHJpZXM6IHt9IH07XG4gICAgICB9XG5cbiAgICAgIGNvbnN0IG5leHQgPSBjdXJyZW50LmVudHJpZXNbcGFydF07XG4gICAgICBpZiAobmV4dC50eXBlICE9PSBcImRpclwiKSB7XG4gICAgICAgIHRocm93IG5ldyBFcnJvcihgXCIke3BhcnR9XCIgaXMgbm90IGEgZGlyZWN0b3J5YCk7XG4gICAgICB9XG5cbiAgICAgIGN1cnJlbnQgPSBuZXh0O1xuICAgIH1cblxuICAgIGNvbnN0IGZpbGVOb2RlOiBGaWxlTm9kZSA9IHsgdHlwZTogXCJmaWxlXCIsIGNvbnRlbnQ6IG5ldyBVaW50OEFycmF5KDApIH07XG4gICAgY3VycmVudC5lbnRyaWVzW2ZpbGVOYW1lXSA9IGZpbGVOb2RlO1xuICAgIHJldHVybiBmaWxlTm9kZTtcbiAgfVxuXG4gIC8qKlxuICAgKiBOb3JtYWxpemVzIGEgcGF0aCBieSByZW1vdmluZyBkdXBsaWNhdGUgc2xhc2hlcyBhbmQgdHJhaWxpbmcgc2xhc2hlcy5cbiAgICogQHBhcmFtIHBhdGggUGF0aCB0byBub3JtYWxpemVcbiAgICogQHJldHVybnMgTm9ybWFsaXplZCBwYXRoXG4gICAqL1xuICBwcml2YXRlIG5vcm1hbGl6ZVBhdGgocGF0aDogc3RyaW5nKTogc3RyaW5nIHtcbiAgICAvLyBIYW5kbGUgZW1wdHkgcGF0aFxuICAgIGlmICghcGF0aCkgcmV0dXJuIFwiL1wiO1xuXG4gICAgLy8gRW5zdXJlIHBhdGggc3RhcnRzIHdpdGggYSBzbGFzaFxuICAgIGNvbnN0IHdpdGhMZWFkaW5nU2xhc2ggPSBwYXRoLnN0YXJ0c1dpdGgoXCIvXCIpID8gcGF0aCA6IGAvJHtwYXRofWA7XG5cbiAgICAvLyBSZW1vdmUgZHVwbGljYXRlIHNsYXNoZXMgYW5kIG5vcm1hbGl6ZVxuICAgIGNvbnN0IG5vcm1hbGl6ZWQgPSB3aXRoTGVhZGluZ1NsYXNoLnJlcGxhY2UoL1xcLysvZywgXCIvXCIpO1xuXG4gICAgLy8gUmVtb3ZlIHRyYWlsaW5nIHNsYXNoIHVubGVzcyBpdCdzIHRoZSByb290IHBhdGhcbiAgICByZXR1cm4gbm9ybWFsaXplZCA9PT0gXCIvXCIgPyBub3JtYWxpemVkIDogbm9ybWFsaXplZC5yZXBsYWNlKC9cXC8rJC8sIFwiXCIpO1xuICB9XG59XG5cbi8qKlxuICogQ3JlYXRlcyBhIGZlYXR1cmUgcHJvdmlkZXIgdGhhdCBpbXBsZW1lbnRzIGEgY29tcGxldGUgaW4tbWVtb3J5IGZpbGUgc3lzdGVtLlxuICpcbiAqIFRoaXMgcHJvdmlkZXMgaW1wbGVtZW50YXRpb25zIGZvciBhbGwgZmlsZSBkZXNjcmlwdG9yIGFuZCBwYXRoLXJlbGF0ZWQgV0FTSVxuICogZnVuY3Rpb25zLCBpbmNsdWRpbmcgYGZkX3JlYWRgLCBgZmRfd3JpdGVgLCBgZmRfc2Vla2AsIGBmZF90ZWxsYCwgYGZkX2Nsb3NlYCxcbiAqIGBwYXRoX29wZW5gLCBhbmQgbW9yZSB0byBzdXBwb3J0IGEgZnVsbCBmZWF0dXJlZCBmaWxlIHN5c3RlbSBlbnZpcm9ubWVudC5cbiAqXG4gKiBgYGBqc1xuICogY29uc3Qgd2FzaSA9IG5ldyBXQVNJKHtcbiAqICAgZmVhdHVyZXM6IFt1c2VNZW1vcnlGUygpXSxcbiAqIH0pO1xuICogYGBgXG4gKlxuICogWW91IGNhbiBwcm92aWRlIGEgcHJlLWNvbmZpZ3VyZWQgZmlsZSBzeXN0ZW0gaW5zdGFuY2U6XG4gKlxuICogYGBganNcbiAqIGNvbnN0IGZzID0gbmV3IE1lbW9yeUZpbGVTeXN0ZW0oKTtcbiAqIGZzLmFkZEZpbGUoXCIvaGVsbG8udHh0XCIsIFwiSGVsbG8sIHdvcmxkIVwiKTtcbiAqXG4gKiBjb25zdCB3YXNpID0gbmV3IFdBU0koe1xuICogICBmZWF0dXJlczogW3VzZU1lbW9yeUZTKHsgd2l0aEZpbGVTeXN0ZW06IGZzIH0pXSxcbiAqIH0pO1xuICogYGBgXG4gKlxuICogWW91IGNhbiBhbHNvIGNvbWJpbmUgaXQgd2l0aCBzdGFuZGFyZCBJTzpcbiAqXG4gKiBgYGBqc1xuICogY29uc3Qgd2FzaSA9IG5ldyBXQVNJKHtcbiAqICAgZmVhdHVyZXM6IFtcbiAqICAgICB1c2VNZW1vcnlGUyh7XG4gKiAgICAgICB3aXRoU3RkSW86IHtcbiAqICAgICAgICAgc3Rkb3V0OiAobGluZXMpID0+IGRvY3VtZW50LndyaXRlKGxpbmVzKSxcbiAqICAgICAgICAgc3RkZXJyOiAobGluZXMpID0+IGRvY3VtZW50LndyaXRlKGxpbmVzKSxcbiAqICAgICAgIH1cbiAqICAgICB9KVxuICogICBdLFxuICogfSk7XG4gKiBgYGBcbiAqXG4gKiBAcGFyYW0gdXNlT3B0aW9ucyAtIENvbmZpZ3VyYXRpb24gb3B0aW9ucyBmb3IgdGhlIG1lbW9yeSBmaWxlIHN5c3RlbVxuICogQHBhcmFtIHVzZU9wdGlvbnMud2l0aEZpbGVTeXN0ZW0gLSBPcHRpb25hbCBwcmUtY29uZmlndXJlZCBmaWxlIHN5c3RlbSBpbnN0YW5jZVxuICogQHBhcmFtIHVzZU9wdGlvbnMud2l0aFN0ZElvIC0gT3B0aW9uYWwgc3RhbmRhcmQgSS9PIGNvbmZpZ3VyYXRpb25cbiAqIEByZXR1cm5zIEEgV0FTSSBmZWF0dXJlIHByb3ZpZGVyIGltcGxlbWVudGluZyBmaWxlIHN5c3RlbSBmdW5jdGlvbmFsaXR5XG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VNZW1vcnlGUyhcbiAgdXNlT3B0aW9uczoge1xuICAgIHdpdGhGaWxlU3lzdGVtPzogTWVtb3J5RmlsZVN5c3RlbTtcbiAgICB3aXRoU3RkSW8/OiBTdGRJb09wdGlvbnM7XG4gIH0gPSB7fVxuKTogV0FTSUZlYXR1cmVQcm92aWRlciB7XG4gIHJldHVybiAoXG4gICAgd2FzaU9wdGlvbnM6IFdBU0lPcHRpb25zLFxuICAgIGFiaTogV0FTSUFiaSxcbiAgICBtZW1vcnlWaWV3OiAoKSA9PiBEYXRhVmlld1xuICApID0+IHtcbiAgICBjb25zdCBmaWxlU3lzdGVtID1cbiAgICAgIHVzZU9wdGlvbnMud2l0aEZpbGVTeXN0ZW0gfHwgbmV3IE1lbW9yeUZpbGVTeXN0ZW0od2FzaU9wdGlvbnMucHJlb3BlbnMpO1xuICAgIGNvbnN0IGZpbGVzOiB7IFtmZDogRmlsZURlc2NyaXB0b3JdOiBPcGVuRmlsZSB9ID0ge307XG5cbiAgICBmdW5jdGlvbiBnZXRGaWxlVGltZXMobm9kZTogRlNOb2RlKTogeyBhdGltOiBiaWdpbnQ7IG10aW06IGJpZ2ludDsgY3RpbTogYmlnaW50IH0ge1xuICAgICAgaWYgKG5vZGUudHlwZSA9PT0gXCJmaWxlXCIgJiYgbm9kZS5jb250ZW50IGluc3RhbmNlb2YgQmxvYikge1xuICAgICAgICBjb25zdCBtdGltTnMgPSBCaWdJbnQoKG5vZGUuY29udGVudCBhcyBGaWxlKS5sYXN0TW9kaWZpZWQgPz8gRGF0ZS5ub3coKSkgKiAxXzAwMF8wMDBuO1xuICAgICAgICByZXR1cm4geyBhdGltOiBtdGltTnMsIG10aW06IG10aW1OcywgY3RpbTogbXRpbU5zIH07XG4gICAgICB9XG4gICAgICByZXR1cm4geyBhdGltOiAwbiwgbXRpbTogMG4sIGN0aW06IDBuIH07XG4gICAgfVxuXG4gICAgYmluZFN0ZGlvKHVzZU9wdGlvbnMud2l0aFN0ZElvIHx8IHt9KS5mb3JFYWNoKChlbnRyeSwgZmQpID0+IHtcbiAgICAgIGZpbGVzW2ZkXSA9IHtcbiAgICAgICAgbm9kZTogeyB0eXBlOiBcImNoYXJhY3RlclwiLCBraW5kOiBcInN0ZGlvXCIsIGVudHJ5IH0sXG4gICAgICAgIHBvc2l0aW9uOiAwLFxuICAgICAgICBpc1ByZW9wZW46IGZhbHNlLFxuICAgICAgICBwYXRoOiBgL2Rldi9mZC8ke2ZkfWAsXG4gICAgICAgIGZkLFxuICAgICAgfTtcbiAgICB9KTtcblxuICAgIGxldCBuZXh0RmQgPSAzO1xuICAgIGZvciAoY29uc3QgcHJlb3BlblBhdGggb2YgZmlsZVN5c3RlbS5nZXRQcmVvcGVuUGF0aHMoKSkge1xuICAgICAgY29uc3Qgbm9kZSA9IGZpbGVTeXN0ZW0ubG9va3VwKHByZW9wZW5QYXRoKTtcbiAgICAgIGlmIChub2RlICYmIG5vZGUudHlwZSA9PT0gXCJkaXJcIikge1xuICAgICAgICBmaWxlc1tuZXh0RmRdID0ge1xuICAgICAgICAgIG5vZGUsXG4gICAgICAgICAgcG9zaXRpb246IDAsXG4gICAgICAgICAgaXNQcmVvcGVuOiB0cnVlLFxuICAgICAgICAgIHByZW9wZW5QYXRoLFxuICAgICAgICAgIHBhdGg6IHByZW9wZW5QYXRoLFxuICAgICAgICAgIGZkOiBuZXh0RmQsXG4gICAgICAgIH07XG4gICAgICAgIG5leHRGZCsrO1xuICAgICAgfVxuICAgIH1cblxuICAgIGZ1bmN0aW9uIGdldEZpbGVGcm9tUGF0aChndWVzdFBhdGg6IHN0cmluZyk6IE9wZW5GaWxlIHwgbnVsbCB7XG4gICAgICBmb3IgKGNvbnN0IGZkIGluIGZpbGVzKSB7XG4gICAgICAgIGNvbnN0IGZpbGUgPSBmaWxlc1tmZF07XG4gICAgICAgIGlmIChmaWxlPy5wYXRoID09PSBndWVzdFBhdGgpIHJldHVybiBmaWxlO1xuICAgICAgfVxuICAgICAgcmV0dXJuIG51bGw7XG4gICAgfVxuXG4gICAgZnVuY3Rpb24gZ2V0RmlsZUZyb21GRChmaWxlRGVzY3JpcHRvcjogRmlsZURlc2NyaXB0b3IpOiBPcGVuRmlsZSB8IG51bGwge1xuICAgICAgY29uc3QgZmlsZSA9IGZpbGVzW2ZpbGVEZXNjcmlwdG9yXTtcbiAgICAgIHJldHVybiBmaWxlIHx8IG51bGw7XG4gICAgfVxuXG4gICAgZnVuY3Rpb24gZ2V0RmlsZVNpemUoZmlsZTogRmlsZU5vZGUpOiBudW1iZXIge1xuICAgICAgaWYgKGZpbGUuY29udGVudCBpbnN0YW5jZW9mIEJsb2IpIHtcbiAgICAgICAgcmV0dXJuIGZpbGUuY29udGVudC5zaXplO1xuICAgICAgfVxuICAgICAgcmV0dXJuIGZpbGUuY29udGVudC5ieXRlTGVuZ3RoO1xuICAgIH1cblxuICAgIHJldHVybiB7XG4gICAgICBmZF9yZWFkOiBhc3luYyAoXG4gICAgICAgIGZkOiBudW1iZXIsXG4gICAgICAgIGlvdnM6IG51bWJlcixcbiAgICAgICAgaW92c0xlbjogbnVtYmVyLFxuICAgICAgICBucmVhZDogbnVtYmVyXG4gICAgICApID0+IHtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgY29uc3QgaW92Vmlld3MgPSBhYmkuaW92Vmlld3ModmlldywgaW92cywgaW92c0xlbik7XG4gICAgICAgIGNvbnN0IGZpbGUgPSBnZXRGaWxlRnJvbUZEKGZkKTtcbiAgICAgICAgaWYgKCFmaWxlKSB7XG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuICAgICAgICB9XG5cbiAgICAgICAgaWYgKGZpbGUubm9kZS50eXBlID09PSBcImNoYXJhY3RlclwiICYmIGZpbGUubm9kZS5raW5kID09PSBcInN0ZGlvXCIpIHtcbiAgICAgICAgICBjb25zdCBieXRlc1JlYWQgPSBmaWxlLm5vZGUuZW50cnkucmVhZHYoaW92Vmlld3MpO1xuICAgICAgICAgIHZpZXcuc2V0VWludDMyKG5yZWFkLCBieXRlc1JlYWQsIHRydWUpO1xuICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH1cblxuICAgICAgICBpZiAoZmlsZS5ub2RlLnR5cGUgPT09IFwiZGlyXCIpIHtcbiAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lTRElSO1xuICAgICAgICB9XG5cbiAgICAgICAgaWYgKGZpbGUubm9kZS50eXBlID09PSBcImNoYXJhY3RlclwiICYmIGZpbGUubm9kZS5raW5kID09PSBcImRldm51bGxcIikge1xuICAgICAgICAgIHZpZXcuc2V0VWludDMyKG5yZWFkLCAwLCB0cnVlKTtcbiAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgICB9XG5cbiAgICAgICAgY29uc3QgZmlsZU5vZGUgPSBmaWxlLm5vZGU7XG4gICAgICAgIGNvbnN0IGRhdGEgPSBmaWxlTm9kZS5jb250ZW50O1xuICAgICAgICBjb25zdCBhdmFpbGFibGUgPSBnZXRGaWxlU2l6ZShmaWxlTm9kZSkgLSBmaWxlLnBvc2l0aW9uO1xuXG4gICAgICAgIGxldCB0b3RhbFJlYWQgPSAwO1xuICAgICAgICBpZiAoYXZhaWxhYmxlIDw9IDApIHtcbiAgICAgICAgICB2aWV3LnNldFVpbnQzMihucmVhZCwgMCwgdHJ1ZSk7XG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgICAgfVxuXG4gICAgICAgIGlmIChmaWxlTm9kZS5jb250ZW50IGluc3RhbmNlb2YgQmxvYikge1xuICAgICAgICAgIGNvbnN0IGJsb2IgPSBmaWxlTm9kZS5jb250ZW50O1xuICAgICAgICAgIGZvciAoY29uc3QgYnVmIG9mIGlvdlZpZXdzKSB7XG4gICAgICAgICAgICBpZiAoZmlsZS5wb3NpdGlvbiA+PSBibG9iLnNpemUpIGJyZWFrO1xuICAgICAgICAgICAgY29uc3QgYnl0ZXNUb1JlYWQgPSBNYXRoLm1pbihcbiAgICAgICAgICAgICAgYnVmLmJ5dGVMZW5ndGgsXG4gICAgICAgICAgICAgIGJsb2Iuc2l6ZSAtIGZpbGUucG9zaXRpb25cbiAgICAgICAgICAgICk7XG4gICAgICAgICAgICBpZiAoYnl0ZXNUb1JlYWQgPD0gMCkgYnJlYWs7XG4gICAgICAgICAgICBjb25zdCBjaHVuayA9IGF3YWl0IGJsb2JcbiAgICAgICAgICAgICAgLnNsaWNlKGZpbGUucG9zaXRpb24sIGZpbGUucG9zaXRpb24gKyBieXRlc1RvUmVhZClcbiAgICAgICAgICAgICAgLmFycmF5QnVmZmVyKCk7XG4gICAgICAgICAgICBidWYuc2V0KG5ldyBVaW50OEFycmF5KGNodW5rKSk7XG4gICAgICAgICAgICB0b3RhbFJlYWQgKz0gY2h1bmsuYnl0ZUxlbmd0aDtcbiAgICAgICAgICAgIGZpbGUucG9zaXRpb24gKz0gY2h1bmsuYnl0ZUxlbmd0aDtcbiAgICAgICAgICB9XG4gICAgICAgIH0gZWxzZSBpZiAoQXJyYXlCdWZmZXIuaXNWaWV3KGRhdGEpKSB7XG4gICAgICAgICAgZm9yIChjb25zdCBidWYgb2YgaW92Vmlld3MpIHtcbiAgICAgICAgICAgIGlmIChmaWxlLnBvc2l0aW9uID49IGRhdGEuYnl0ZUxlbmd0aCkgYnJlYWs7XG4gICAgICAgICAgICBjb25zdCBieXRlc1RvUmVhZCA9IE1hdGgubWluKFxuICAgICAgICAgICAgICBidWYuYnl0ZUxlbmd0aCxcbiAgICAgICAgICAgICAgZGF0YS5ieXRlTGVuZ3RoIC0gZmlsZS5wb3NpdGlvblxuICAgICAgICAgICAgKTtcbiAgICAgICAgICAgIGlmIChieXRlc1RvUmVhZCA8PSAwKSBicmVhaztcbiAgICAgICAgICAgIGJ1Zi5zZXQoZGF0YS5zbGljZShmaWxlLnBvc2l0aW9uLCBmaWxlLnBvc2l0aW9uICsgYnl0ZXNUb1JlYWQpKTtcbiAgICAgICAgICAgIHRvdGFsUmVhZCArPSBieXRlc1RvUmVhZDtcbiAgICAgICAgICAgIGZpbGUucG9zaXRpb24gKz0gYnl0ZXNUb1JlYWQ7XG4gICAgICAgICAgfVxuICAgICAgICB9XG5cbiAgICAgICAgdmlldy5zZXRVaW50MzIobnJlYWQsIHRvdGFsUmVhZCwgdHJ1ZSk7XG4gICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICB9LFxuXG4gICAgICBmZF93cml0ZTogKFxuICAgICAgICBmZDogbnVtYmVyLFxuICAgICAgICBpb3ZzOiBudW1iZXIsXG4gICAgICAgIGlvdnNMZW46IG51bWJlcixcbiAgICAgICAgbndyaXR0ZW46IG51bWJlclxuICAgICAgKSA9PiB7XG4gICAgICAgIGNvbnN0IHZpZXcgPSBtZW1vcnlWaWV3KCk7XG4gICAgICAgIGNvbnN0IGlvdlZpZXdzID0gYWJpLmlvdlZpZXdzKHZpZXcsIGlvdnMsIGlvdnNMZW4pO1xuICAgICAgICBjb25zdCBmaWxlID0gZ2V0RmlsZUZyb21GRChmZCk7XG4gICAgICAgIGlmICghZmlsZSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuICAgICAgICBsZXQgdG90YWxXcml0dGVuID0gMDtcblxuICAgICAgICBpZiAoZmlsZS5ub2RlLnR5cGUgPT09IFwiY2hhcmFjdGVyXCIgJiYgZmlsZS5ub2RlLmtpbmQgPT09IFwic3RkaW9cIikge1xuICAgICAgICAgIGNvbnN0IGJ5dGVzV3JpdHRlbiA9IGZpbGUubm9kZS5lbnRyeS53cml0ZXYoaW92Vmlld3MpO1xuICAgICAgICAgIHZpZXcuc2V0VWludDMyKG53cml0dGVuLCBieXRlc1dyaXR0ZW4sIHRydWUpO1xuICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH1cblxuICAgICAgICBpZiAoZmlsZS5ub2RlLnR5cGUgPT09IFwiZGlyXCIpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fSVNESVI7XG5cbiAgICAgICAgaWYgKGZpbGUubm9kZS50eXBlID09PSBcImNoYXJhY3RlclwiICYmIGZpbGUubm9kZS5raW5kID09PSBcImRldm51bGxcIikge1xuICAgICAgICAgIGNvbnN0IHRvdGFsID0gaW92Vmlld3MucmVkdWNlKChhY2MsIGJ1ZikgPT4gYWNjICsgYnVmLmJ5dGVMZW5ndGgsIDApO1xuICAgICAgICAgIHZpZXcuc2V0VWludDMyKG53cml0dGVuLCB0b3RhbCwgdHJ1ZSk7XG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgICAgfVxuXG4gICAgICAgIGlmIChmaWxlLm5vZGUuY29udGVudCBpbnN0YW5jZW9mIEJsb2IpIHtcbiAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lOVkFMO1xuICAgICAgICB9XG5cbiAgICAgICAgbGV0IHBvcyA9IGZpbGUucG9zaXRpb247XG4gICAgICAgIGNvbnN0IGRhdGFUb1dyaXRlID0gaW92Vmlld3MucmVkdWNlKFxuICAgICAgICAgIChhY2MsIGJ1ZikgPT4gYWNjICsgYnVmLmJ5dGVMZW5ndGgsXG4gICAgICAgICAgMFxuICAgICAgICApO1xuICAgICAgICBjb25zdCByZXF1aXJlZExlbmd0aCA9IHBvcyArIGRhdGFUb1dyaXRlO1xuICAgICAgICBsZXQgbmV3Q29udGVudDogVWludDhBcnJheTtcblxuICAgICAgICBpZiAocmVxdWlyZWRMZW5ndGggPiBnZXRGaWxlU2l6ZShmaWxlLm5vZGUpKSB7XG4gICAgICAgICAgbmV3Q29udGVudCA9IG5ldyBVaW50OEFycmF5KHJlcXVpcmVkTGVuZ3RoKTtcbiAgICAgICAgICBuZXdDb250ZW50LnNldChmaWxlLm5vZGUuY29udGVudCwgMCk7XG4gICAgICAgIH0gZWxzZSB7XG4gICAgICAgICAgbmV3Q29udGVudCA9IGZpbGUubm9kZS5jb250ZW50O1xuICAgICAgICB9XG5cbiAgICAgICAgZm9yIChjb25zdCBidWYgb2YgaW92Vmlld3MpIHtcbiAgICAgICAgICBuZXdDb250ZW50LnNldChidWYsIHBvcyk7XG4gICAgICAgICAgcG9zICs9IGJ1Zi5ieXRlTGVuZ3RoO1xuICAgICAgICAgIHRvdGFsV3JpdHRlbiArPSBidWYuYnl0ZUxlbmd0aDtcbiAgICAgICAgfVxuXG4gICAgICAgIGZpbGUubm9kZS5jb250ZW50ID0gbmV3Q29udGVudDtcbiAgICAgICAgZmlsZS5wb3NpdGlvbiA9IHBvcztcbiAgICAgICAgdmlldy5zZXRVaW50MzIobndyaXR0ZW4sIHRvdGFsV3JpdHRlbiwgdHJ1ZSk7XG4gICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICB9LFxuXG4gICAgICBmZF9jbG9zZTogKGZkOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgZmlsZSA9IGdldEZpbGVGcm9tRkQoZmQpO1xuICAgICAgICBpZiAoIWZpbGUpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcblxuICAgICAgICBpZiAoZmlsZS5ub2RlLnR5cGUgPT09IFwiY2hhcmFjdGVyXCIgJiYgZmlsZS5ub2RlLmtpbmQgPT09IFwic3RkaW9cIikge1xuICAgICAgICAgIGZpbGUubm9kZS5lbnRyeS5jbG9zZSgpO1xuICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH1cblxuICAgICAgICBkZWxldGUgZmlsZXNbZmRdO1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcblxuICAgICAgZmRfc2VlazogKGZkOiBudW1iZXIsIG9mZnNldDogYmlnaW50LCB3aGVuY2U6IG51bWJlciwgbmV3T2Zmc2V0UHRyOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcblxuICAgICAgICBjb25zdCBmaWxlID0gZ2V0RmlsZUZyb21GRChmZCk7XG4gICAgICAgIGlmICghZmlsZSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuXG4gICAgICAgIGlmIChmaWxlLm5vZGUudHlwZSA9PT0gXCJkaXJcIikgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19JU0RJUjtcbiAgICAgICAgaWYgKGZpbGUubm9kZS50eXBlID09PSBcImNoYXJhY3RlclwiKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lPO1xuXG4gICAgICAgIGNvbnN0IGZpbGVMZW5ndGggPSBnZXRGaWxlU2l6ZShmaWxlLm5vZGUpO1xuICAgICAgICBsZXQgbmV3UG9zaXRpb246IG51bWJlcjtcblxuICAgICAgICBzd2l0Y2ggKHdoZW5jZSkge1xuICAgICAgICAgIGNhc2UgMDogLy8gV0hFTkNFX1NFVFxuICAgICAgICAgICAgbmV3UG9zaXRpb24gPSBOdW1iZXIob2Zmc2V0KTtcbiAgICAgICAgICAgIGJyZWFrO1xuICAgICAgICAgIGNhc2UgMTogLy8gV0hFTkNFX0NVUlxuICAgICAgICAgICAgbmV3UG9zaXRpb24gPSBmaWxlLnBvc2l0aW9uICsgTnVtYmVyKG9mZnNldCk7XG4gICAgICAgICAgICBicmVhaztcbiAgICAgICAgICBjYXNlIDI6IC8vIFdIRU5DRV9FTkRcbiAgICAgICAgICAgIG5ld1Bvc2l0aW9uID0gZmlsZUxlbmd0aCArIE51bWJlcihvZmZzZXQpO1xuICAgICAgICAgICAgYnJlYWs7XG4gICAgICAgICAgZGVmYXVsdDpcbiAgICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fSU5WQUw7XG4gICAgICAgIH1cblxuICAgICAgICBpZiAobmV3UG9zaXRpb24gPCAwKSB7XG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19JTlZBTDtcbiAgICAgICAgfVxuXG4gICAgICAgIGZpbGUucG9zaXRpb24gPSBuZXdQb3NpdGlvbjtcbiAgICAgICAgdmlldy5zZXRCaWdVaW50NjQobmV3T2Zmc2V0UHRyLCBCaWdJbnQobmV3UG9zaXRpb24pLCB0cnVlKTtcblxuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcblxuICAgICAgZmRfdGVsbDogKGZkOiBudW1iZXIsIG9mZnNldF9wdHI6IG51bWJlcikgPT4ge1xuICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuXG4gICAgICAgIGNvbnN0IGZpbGUgPSBnZXRGaWxlRnJvbUZEKGZkKTtcbiAgICAgICAgaWYgKCFmaWxlKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG5cbiAgICAgICAgaWYgKGZpbGUubm9kZS50eXBlID09PSBcImRpclwiKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lPO1xuICAgICAgICBpZiAoZmlsZS5ub2RlLnR5cGUgPT09IFwiY2hhcmFjdGVyXCIpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fSU87XG5cbiAgICAgICAgdmlldy5zZXRCaWdVaW50NjQob2Zmc2V0X3B0ciwgQmlnSW50KGZpbGUucG9zaXRpb24pLCB0cnVlKTtcbiAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgIH0sXG5cbiAgICAgIGZkX2Zkc3RhdF9nZXQ6IChmZDogbnVtYmVyLCBidWY6IG51bWJlcikgPT4ge1xuICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuICAgICAgICBjb25zdCBmaWxlID0gZ2V0RmlsZUZyb21GRChmZCk7XG4gICAgICAgIGlmICghZmlsZSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuXG4gICAgICAgIGxldCBmaWxldHlwZTogbnVtYmVyO1xuICAgICAgICBzd2l0Y2ggKGZpbGUubm9kZS50eXBlKSB7XG4gICAgICAgICAgY2FzZSBcImNoYXJhY3RlclwiOlxuICAgICAgICAgICAgZmlsZXR5cGUgPSBXQVNJQWJpLldBU0lfRklMRVRZUEVfQ0hBUkFDVEVSX0RFVklDRTtcbiAgICAgICAgICAgIGJyZWFrO1xuICAgICAgICAgIGNhc2UgXCJkaXJcIjpcbiAgICAgICAgICAgIGZpbGV0eXBlID0gV0FTSUFiaS5XQVNJX0ZJTEVUWVBFX0RJUkVDVE9SWTtcbiAgICAgICAgICAgIGJyZWFrO1xuICAgICAgICAgIGNhc2UgXCJmaWxlXCI6XG4gICAgICAgICAgICBmaWxldHlwZSA9IFdBU0lBYmkuV0FTSV9GSUxFVFlQRV9SRUdVTEFSX0ZJTEU7XG4gICAgICAgICAgICBicmVhaztcbiAgICAgICAgfVxuXG4gICAgICAgIGNvbnN0IGFsbFJpZ2h0cyA9IDB4MWZmZmZmZmZuO1xuICAgICAgICBhYmkud3JpdGVGZHN0YXQodmlldywgYnVmLCBmaWxldHlwZSwgMCwgYWxsUmlnaHRzLCBhbGxSaWdodHMpO1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcblxuICAgICAgZmRfZmlsZXN0YXRfZ2V0OiAoZmQ6IG51bWJlciwgYnVmOiBudW1iZXIpID0+IHtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgY29uc3QgZW50cnkgPSBnZXRGaWxlRnJvbUZEKGZkKTtcbiAgICAgICAgaWYgKCFlbnRyeSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19CQURGO1xuXG4gICAgICAgIGxldCBmaWxldHlwZTogbnVtYmVyO1xuICAgICAgICBsZXQgc2l6ZSA9IDA7XG4gICAgICAgIHN3aXRjaCAoZW50cnkubm9kZS50eXBlKSB7XG4gICAgICAgICAgY2FzZSBcImNoYXJhY3RlclwiOlxuICAgICAgICAgICAgZmlsZXR5cGUgPSBXQVNJQWJpLldBU0lfRklMRVRZUEVfQ0hBUkFDVEVSX0RFVklDRTtcbiAgICAgICAgICAgIGJyZWFrO1xuICAgICAgICAgIGNhc2UgXCJkaXJcIjpcbiAgICAgICAgICAgIGZpbGV0eXBlID0gV0FTSUFiaS5XQVNJX0ZJTEVUWVBFX0RJUkVDVE9SWTtcbiAgICAgICAgICAgIGJyZWFrO1xuICAgICAgICAgIGNhc2UgXCJmaWxlXCI6XG4gICAgICAgICAgICBmaWxldHlwZSA9IFdBU0lBYmkuV0FTSV9GSUxFVFlQRV9SRUdVTEFSX0ZJTEU7XG4gICAgICAgICAgICBzaXplID0gZ2V0RmlsZVNpemUoZW50cnkubm9kZSk7XG4gICAgICAgICAgICBicmVhaztcbiAgICAgICAgfVxuXG4gICAgICAgIGNvbnN0IHsgYXRpbSwgbXRpbSwgY3RpbSB9ID0gZ2V0RmlsZVRpbWVzKGVudHJ5Lm5vZGUpO1xuICAgICAgICBhYmkud3JpdGVGaWxlc3RhdCh2aWV3LCBidWYsIGZpbGV0eXBlLCBCaWdJbnQoc2l6ZSksIGF0aW0sIG10aW0sIGN0aW0pO1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcblxuICAgICAgZmRfcHJlc3RhdF9nZXQ6IChmZDogbnVtYmVyLCBidWY6IG51bWJlcikgPT4ge1xuICAgICAgICBjb25zdCB2aWV3ID0gbWVtb3J5VmlldygpO1xuICAgICAgICBpZiAoZmQgPCAzKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG5cbiAgICAgICAgY29uc3QgZmlsZSA9IGdldEZpbGVGcm9tRkQoZmQpO1xuXG4gICAgICAgIGlmICghZmlsZSB8fCAhZmlsZS5pc1ByZW9wZW4pIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fQkFERjtcblxuICAgICAgICB2aWV3LnNldFVpbnQ4KGJ1ZiwgMCk7XG4gICAgICAgIGNvbnN0IHBhdGhTdHIgPSBmaWxlLnByZW9wZW5QYXRoIHx8IFwiXCI7XG4gICAgICAgIHZpZXcuc2V0VWludDMyKGJ1ZiArIDQsIHBhdGhTdHIubGVuZ3RoLCB0cnVlKTtcbiAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgIH0sXG5cbiAgICAgIGZkX3ByZXN0YXRfZGlyX25hbWU6IChmZDogbnVtYmVyLCBwYXRoUHRyOiBudW1iZXIsIHBhdGhMZW46IG51bWJlcikgPT4ge1xuICAgICAgICBpZiAoZmQgPCAzKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG5cbiAgICAgICAgY29uc3QgZmlsZSA9IGdldEZpbGVGcm9tRkQoZmQpO1xuICAgICAgICBpZiAoIWZpbGUgfHwgIWZpbGUuaXNQcmVvcGVuKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG5cbiAgICAgICAgY29uc3QgcGF0aFN0ciA9IGZpbGUucHJlb3BlblBhdGggfHwgXCJcIjtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcbiAgICAgICAgY29uc3QgbGVuID0gTWF0aC5taW4ocGF0aFN0ci5sZW5ndGgsIHBhdGhMZW4pO1xuICAgICAgICBmb3IgKGxldCBpID0gMDsgaSA8IGxlbjsgaSsrKSB7XG4gICAgICAgICAgdmlldy5zZXRVaW50OChwYXRoUHRyICsgaSwgcGF0aFN0ci5jaGFyQ29kZUF0KGkpKTtcbiAgICAgICAgfVxuXG4gICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICB9LFxuXG4gICAgICBmZF9vcGVuOiAoXG4gICAgICAgIGRpcmZkOiBudW1iZXIsXG4gICAgICAgIHBhdGhQdHI6IG51bWJlcixcbiAgICAgICAgcGF0aExlbjogbnVtYmVyLFxuICAgICAgICBvZmxhZ3M6IG51bWJlcixcbiAgICAgICAgX2ZzX3JpZ2h0c19iYXNlOiBiaWdpbnQsXG4gICAgICAgIF9mc19yaWdodHNfaW5oZXJpdGluZzogYmlnaW50LFxuICAgICAgICBfZmRmbGFnczogbnVtYmVyLFxuICAgICAgICBvcGVuZWRfZmQ6IG51bWJlclxuICAgICAgKSA9PiB7XG4gICAgICAgIGNvbnN0IHZpZXcgPSBtZW1vcnlWaWV3KCk7XG5cbiAgICAgICAgaWYgKGRpcmZkIDwgMykgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19OT1RESVI7XG5cbiAgICAgICAgY29uc3QgZGlyRW50cnkgPSBnZXRGaWxlRnJvbUZEKGRpcmZkKTtcbiAgICAgICAgaWYgKCFkaXJFbnRyeSB8fCBkaXJFbnRyeS5ub2RlLnR5cGUgIT09IFwiZGlyXCIpXG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19OT1RESVI7XG5cbiAgICAgICAgY29uc3QgcGF0aCA9IGFiaS5yZWFkU3RyaW5nKHZpZXcsIHBhdGhQdHIsIHBhdGhMZW4pO1xuXG4gICAgICAgIGNvbnN0IGd1ZXN0UGF0aCA9XG4gICAgICAgICAgKGRpckVudHJ5LnBhdGguZW5kc1dpdGgoXCIvXCIpID8gZGlyRW50cnkucGF0aCA6IGAke2RpckVudHJ5LnBhdGh9L2ApICtcbiAgICAgICAgICBwYXRoO1xuXG4gICAgICAgIGNvbnN0IGV4aXN0aW5nID0gZ2V0RmlsZUZyb21QYXRoKGd1ZXN0UGF0aCk7XG4gICAgICAgIGlmIChleGlzdGluZykge1xuICAgICAgICAgIHZpZXcuc2V0VWludDMyKG9wZW5lZF9mZCwgZXhpc3RpbmcuZmQsIHRydWUpO1xuICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH1cblxuICAgICAgICBsZXQgdGFyZ2V0ID0gZmlsZVN5c3RlbS5yZXNvbHZlKGRpckVudHJ5Lm5vZGUsIHBhdGgpO1xuICAgICAgICBjb25zdCBPX0NSRUFUID0gMSA8PCAwO1xuICAgICAgICBjb25zdCBPX0VYQ0wgPSAxIDw8IDE7XG4gICAgICAgIGNvbnN0IE9fVFJVTkMgPSAxIDw8IDI7XG5cbiAgICAgICAgaWYgKHRhcmdldCkge1xuICAgICAgICAgIGlmIChvZmxhZ3MgJiBPX0VYQ0wpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fRVhJU1Q7XG4gICAgICAgICAgaWYgKG9mbGFncyAmIE9fVFJVTkMpIHtcbiAgICAgICAgICAgIGlmICh0YXJnZXQudHlwZSAhPT0gXCJmaWxlXCIpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fSU5WQUw7XG4gICAgICAgICAgICB0YXJnZXQuY29udGVudCA9IG5ldyBVaW50OEFycmF5KDApO1xuICAgICAgICAgIH1cbiAgICAgICAgfSBlbHNlIHtcbiAgICAgICAgICBpZiAoIShvZmxhZ3MgJiBPX0NSRUFUKSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19OT0VOVDtcbiAgICAgICAgICB0YXJnZXQgPSBmaWxlU3lzdGVtLmNyZWF0ZUZpbGVJbihkaXJFbnRyeS5ub2RlLCBwYXRoKTtcbiAgICAgICAgfVxuXG4gICAgICAgIGZpbGVzW25leHRGZF0gPSB7XG4gICAgICAgICAgbm9kZTogdGFyZ2V0LFxuICAgICAgICAgIHBvc2l0aW9uOiAwLFxuICAgICAgICAgIGlzUHJlb3BlbjogZmFsc2UsXG4gICAgICAgICAgcGF0aDogZ3Vlc3RQYXRoLFxuICAgICAgICAgIGZkOiBuZXh0RmQsXG4gICAgICAgIH07XG5cbiAgICAgICAgdmlldy5zZXRVaW50MzIob3BlbmVkX2ZkLCBuZXh0RmQsIHRydWUpO1xuICAgICAgICBuZXh0RmQrKztcbiAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgIH0sXG5cbiAgICAgIHBhdGhfb3BlbjogKFxuICAgICAgICBkaXJmZDogbnVtYmVyLFxuICAgICAgICBfZGlyZmxhZ3M6IG51bWJlcixcbiAgICAgICAgcGF0aFB0cjogbnVtYmVyLFxuICAgICAgICBwYXRoTGVuOiBudW1iZXIsXG4gICAgICAgIG9mbGFnczogbnVtYmVyLFxuICAgICAgICBfZnNfcmlnaHRzX2Jhc2U6IGJpZ2ludCxcbiAgICAgICAgX2ZzX3JpZ2h0c19pbmhlcml0aW5nOiBiaWdpbnQsXG4gICAgICAgIF9mZGZsYWdzOiBudW1iZXIsXG4gICAgICAgIG9wZW5lZF9mZDogbnVtYmVyXG4gICAgICApID0+IHtcbiAgICAgICAgY29uc3QgdmlldyA9IG1lbW9yeVZpZXcoKTtcblxuICAgICAgICBpZiAoZGlyZmQgPCAzKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX05PVERJUjtcblxuICAgICAgICBjb25zdCBkaXJFbnRyeSA9IGdldEZpbGVGcm9tRkQoZGlyZmQpO1xuICAgICAgICBpZiAoIWRpckVudHJ5IHx8IGRpckVudHJ5Lm5vZGUudHlwZSAhPT0gXCJkaXJcIilcbiAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX05PVERJUjtcblxuICAgICAgICBjb25zdCBwYXRoID0gYWJpLnJlYWRTdHJpbmcodmlldywgcGF0aFB0ciwgcGF0aExlbik7XG5cbiAgICAgICAgY29uc3QgZ3Vlc3RQYXRoID1cbiAgICAgICAgICAoZGlyRW50cnkucGF0aC5lbmRzV2l0aChcIi9cIikgPyBkaXJFbnRyeS5wYXRoIDogYCR7ZGlyRW50cnkucGF0aH0vYCkgK1xuICAgICAgICAgIHBhdGg7XG5cbiAgICAgICAgY29uc3QgZXhpc3RpbmcgPSBnZXRGaWxlRnJvbVBhdGgoZ3Vlc3RQYXRoKTtcbiAgICAgICAgaWYgKGV4aXN0aW5nKSB7XG4gICAgICAgICAgdmlldy5zZXRVaW50MzIob3BlbmVkX2ZkLCBleGlzdGluZy5mZCwgdHJ1ZSk7XG4gICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgICAgfVxuXG4gICAgICAgIGxldCB0YXJnZXQgPSBmaWxlU3lzdGVtLnJlc29sdmUoZGlyRW50cnkubm9kZSBhcyBEaXJlY3RvcnlOb2RlLCBwYXRoKTtcbiAgICAgICAgY29uc3QgT19DUkVBVCA9IDEgPDwgMDtcbiAgICAgICAgY29uc3QgT19FWENMID0gMSA8PCAxO1xuICAgICAgICBjb25zdCBPX1RSVU5DID0gMSA8PCAyO1xuXG4gICAgICAgIGlmICh0YXJnZXQpIHtcbiAgICAgICAgICBpZiAob2ZsYWdzICYgT19FWENMKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0VYSVNUO1xuICAgICAgICAgIGlmIChvZmxhZ3MgJiBPX1RSVU5DKSB7XG4gICAgICAgICAgICBpZiAodGFyZ2V0LnR5cGUgIT09IFwiZmlsZVwiKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lOVkFMO1xuICAgICAgICAgICAgKHRhcmdldCBhcyBGaWxlTm9kZSkuY29udGVudCA9IG5ldyBVaW50OEFycmF5KDApO1xuICAgICAgICAgIH1cbiAgICAgICAgfSBlbHNlIHtcbiAgICAgICAgICBpZiAoIShvZmxhZ3MgJiBPX0NSRUFUKSkgcmV0dXJuIFdBU0lBYmkuV0FTSV9FUlJOT19OT0VOVDtcbiAgICAgICAgICB0YXJnZXQgPSBmaWxlU3lzdGVtLmNyZWF0ZUZpbGVJbihcbiAgICAgICAgICAgIGRpckVudHJ5Lm5vZGUgYXMgRGlyZWN0b3J5Tm9kZSxcbiAgICAgICAgICAgIHBhdGhcbiAgICAgICAgICApO1xuICAgICAgICB9XG5cbiAgICAgICAgZmlsZXNbbmV4dEZkXSA9IHtcbiAgICAgICAgICBub2RlOiB0YXJnZXQsXG4gICAgICAgICAgcG9zaXRpb246IDAsXG4gICAgICAgICAgaXNQcmVvcGVuOiBmYWxzZSxcbiAgICAgICAgICBwYXRoOiBndWVzdFBhdGgsXG4gICAgICAgICAgZmQ6IG5leHRGZCxcbiAgICAgICAgfTtcblxuICAgICAgICB2aWV3LnNldFVpbnQzMihvcGVuZWRfZmQsIG5leHRGZCwgdHJ1ZSk7XG4gICAgICAgIG5leHRGZCsrO1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcblxuICAgICAgcGF0aF9maWxlc3RhdF9nZXQ6IChcbiAgICAgICAgZmQ6IG51bWJlcixcbiAgICAgICAgX2ZsYWdzOiBudW1iZXIsXG4gICAgICAgIHBhdGhQdHI6IG51bWJlcixcbiAgICAgICAgcGF0aExlbjogbnVtYmVyLFxuICAgICAgICBidWY6IG51bWJlclxuICAgICAgKSA9PiB7XG4gICAgICAgIGNvbnN0IHZpZXcgPSBtZW1vcnlWaWV3KCk7XG4gICAgICAgIGNvbnN0IGZpbGUgPSBnZXRGaWxlRnJvbUZEKGZkKTtcbiAgICAgICAgaWYgKCFmaWxlKSByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0JBREY7XG4gICAgICAgIGlmIChmaWxlLm5vZGUudHlwZSAhPT0gXCJkaXJcIikge1xuICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fTk9URElSO1xuICAgICAgICB9XG5cbiAgICAgICAgY29uc3QgZ3Vlc3RSZWxQYXRoID0gYWJpLnJlYWRTdHJpbmcodmlldywgcGF0aFB0ciwgcGF0aExlbik7XG4gICAgICAgIGNvbnN0IGJhc2VQYXRoID0gZmlsZS5wYXRoO1xuICAgICAgICBjb25zdCBmdWxsR3Vlc3RQYXRoID0gYmFzZVBhdGguZW5kc1dpdGgoXCIvXCIpXG4gICAgICAgICAgPyBiYXNlUGF0aCArIGd1ZXN0UmVsUGF0aFxuICAgICAgICAgIDogYCR7YmFzZVBhdGh9LyR7Z3Vlc3RSZWxQYXRofWA7XG5cbiAgICAgICAgY29uc3Qgbm9kZSA9IGZpbGVTeXN0ZW0ubG9va3VwKGZ1bGxHdWVzdFBhdGgpO1xuICAgICAgICBpZiAoIW5vZGUpIHJldHVybiBXQVNJQWJpLldBU0lfRVJSTk9fTk9FTlQ7XG4gICAgICAgIGlmIChub2RlLnR5cGUgPT09IFwiY2hhcmFjdGVyXCIgJiYgbm9kZS5raW5kID09PSBcInN0ZGlvXCIpIHtcbiAgICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VSUk5PX0lOVkFMO1xuICAgICAgICB9XG5cbiAgICAgICAgbGV0IGZpbGV0eXBlOiBudW1iZXI7XG4gICAgICAgIGxldCBzaXplID0gMDtcbiAgICAgICAgaWYgKG5vZGUudHlwZSA9PT0gXCJkaXJcIikge1xuICAgICAgICAgIGZpbGV0eXBlID0gV0FTSUFiaS5XQVNJX0ZJTEVUWVBFX0RJUkVDVE9SWTtcbiAgICAgICAgfSBlbHNlIGlmIChub2RlLnR5cGUgPT09IFwiY2hhcmFjdGVyXCIgJiYgbm9kZS5raW5kID09PSBcImRldm51bGxcIikge1xuICAgICAgICAgIGZpbGV0eXBlID0gV0FTSUFiaS5XQVNJX0ZJTEVUWVBFX0NIQVJBQ1RFUl9ERVZJQ0U7XG4gICAgICAgIH0gZWxzZSB7XG4gICAgICAgICAgZmlsZXR5cGUgPSBXQVNJQWJpLldBU0lfRklMRVRZUEVfUkVHVUxBUl9GSUxFO1xuICAgICAgICAgIHNpemUgPSBnZXRGaWxlU2l6ZShub2RlIGFzIEZpbGVOb2RlKTtcbiAgICAgICAgfVxuXG4gICAgICAgIGNvbnN0IHsgYXRpbSwgbXRpbSwgY3RpbSB9ID0gZ2V0RmlsZVRpbWVzKG5vZGUpO1xuICAgICAgICBhYmkud3JpdGVGaWxlc3RhdCh2aWV3LCBidWYsIGZpbGV0eXBlLCBCaWdJbnQoc2l6ZSksIGF0aW0sIG10aW0sIGN0aW0pO1xuICAgICAgICByZXR1cm4gV0FTSUFiaS5XQVNJX0VTVUNDRVNTO1xuICAgICAgfSxcbiAgICB9O1xuICB9O1xufVxuXG5leHBvcnQgZnVuY3Rpb24gdXNlRlMoX3VzZU9wdGlvbnM6IHsgZnM6IHVua25vd24gfSk6IFdBU0lGZWF0dXJlUHJvdmlkZXIge1xuICByZXR1cm4gKF9vcHRpb25zOiBXQVNJT3B0aW9ucywgX2FiaTogV0FTSUFiaSwgX21lbW9yeVZpZXc6ICgpID0+IERhdGFWaWV3KSA9PiB7XG4gICAgLy8gVE9ETzogaW1wbGVtZW50IGZkXyogc3lzY2FsbHMgdXNpbmcgYHVzZU9wdGlvbnMuZnNgXG4gICAgcmV0dXJuIHt9O1xuICB9O1xufVxuIiwKICAgICJpbXBvcnQgeyBXQVNJQWJpLCBXQVNJUHJvY0V4aXQgfSBmcm9tIFwiLi4vYWJpXCI7XG5pbXBvcnQgdHlwZSB7IFdBU0lPcHRpb25zIH0gZnJvbSBcIi4uL29wdGlvbnNcIjtcblxuLyoqXG4gKiBBIGZlYXR1cmUgcHJvdmlkZXIgdGhhdCBwcm92aWRlcyBgcHJvY19leGl0YCBhbmQgYHByb2NfcmFpc2VgIGJ5IEphdmFTY3JpcHQncyBleGNlcHRpb24uXG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VQcm9jKF9vcHRpb25zOiBXQVNJT3B0aW9ucywgX2FiaTogV0FTSUFiaSwgX21lbW9yeVZpZXc6ICgpID0+IERhdGFWaWV3KTogV2ViQXNzZW1ibHkuTW9kdWxlSW1wb3J0cyB7XG4gICAgcmV0dXJuIHtcbiAgICAgICAgcHJvY19leGl0OiAoY29kZTogbnVtYmVyKSA9PiB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgV0FTSVByb2NFeGl0KGNvZGUpO1xuICAgICAgICB9LFxuICAgICAgICBwcm9jX3JhaXNlOiAoX3NpZ25hbDogbnVtYmVyKSA9PiB7XG4gICAgICAgICAgICAvLyBUT0RPOiBJbXBsZW1lbnRcbiAgICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH0sXG4gICAgfTtcbn0iLAogICAgImltcG9ydCB7IFdBU0lBYmkgfSBmcm9tIFwiLi4vYWJpXCI7XG5pbXBvcnQgdHlwZSB7IFdBU0lPcHRpb25zIH0gZnJvbSBcIi4uL29wdGlvbnNcIjtcblxuLyoqXG4gKiBDcmVhdGUgYSBmZWF0dXJlIHByb3ZpZGVyIHRoYXQgcHJvdmlkZXMgYHJhbmRvbV9nZXRgIHdpdGggYGNyeXB0b2AgQVBJcyBhcyBiYWNrZW5kIGJ5IGRlZmF1bHQuXG4gKi9cbmV4cG9ydCBmdW5jdGlvbiB1c2VSYW5kb20oX29wdGlvbnM6IFdBU0lPcHRpb25zLCBfYWJpOiBXQVNJQWJpLCBtZW1vcnlWaWV3OiAoKSA9PiBEYXRhVmlldyk6IFdlYkFzc2VtYmx5Lk1vZHVsZUltcG9ydHMge1xuICAgIHJldHVybiB7XG4gICAgICAgIHJhbmRvbV9nZXQ6IChidWZmZXJPZmZzZXQ6IG51bWJlciwgbGVuZ3RoOiBudW1iZXIpID0+IHtcbiAgICAgICAgICAgIGNvbnN0IHZpZXcgPSBtZW1vcnlWaWV3KCk7XG4gICAgICAgICAgICBjb25zdCBidWZmZXIgPSBuZXcgVWludDhBcnJheSh2aWV3LmJ1ZmZlciwgYnVmZmVyT2Zmc2V0LCBsZW5ndGgpO1xuICAgICAgICAgICAgY3J5cHRvLmdldFJhbmRvbVZhbHVlcyhidWZmZXIpO1xuICAgICAgICAgICAgcmV0dXJuIFdBU0lBYmkuV0FTSV9FU1VDQ0VTUztcbiAgICAgICAgfSxcbiAgICB9O1xufSIsCiAgICAiaW1wb3J0IHsgV0FTSUFiaSwgV0FTSVByb2NFeGl0IH0gZnJvbSBcIi4vYWJpXCI7XG5leHBvcnQgeyBXQVNJUHJvY0V4aXQgfSBmcm9tIFwiLi9hYmlcIjtcbmltcG9ydCB0eXBlIHsgV0FTSU9wdGlvbnMgfSBmcm9tIFwiLi9vcHRpb25zXCI7XG5cbmV4cG9ydCAqIGZyb20gXCIuL2ZlYXR1cmVzL2FyZ3NcIjtcbmV4cG9ydCAqIGZyb20gXCIuL2ZlYXR1cmVzL2Nsb2NrXCI7XG5leHBvcnQgKiBmcm9tIFwiLi9mZWF0dXJlcy9lbnZpcm9uXCI7XG5leHBvcnQgeyB1c2VGUywgdXNlU3RkaW8sIHVzZU1lbW9yeUZTIH0gZnJvbSBcIi4vZmVhdHVyZXMvZmRcIjtcbmV4cG9ydCAqIGZyb20gXCIuL2ZlYXR1cmVzL3Byb2NcIjtcbmV4cG9ydCAqIGZyb20gXCIuL2ZlYXR1cmVzL3JhbmRvbVwiO1xuXG5leHBvcnQgY2xhc3MgV0FTSSB7XG4gICAgLyoqXG4gICAgICogYHdhc2lJbXBvcnRgIGlzIGFuIG9iamVjdCB0aGF0IGltcGxlbWVudHMgdGhlIFdBU0kgc3lzdGVtIGNhbGwgQVBJLiBUaGlzIG9iamVjdFxuICAgICAqIHNob3VsZCBiZSBwYXNzZWQgYXMgdGhlIGB3YXNpX3NuYXBzaG90X3ByZXZpZXcxYCBpbXBvcnQgZHVyaW5nIHRoZSBpbnN0YW50aWF0aW9uXG4gICAgICogb2YgYSBbYFdlYkFzc2VtYmx5Lkluc3RhbmNlYF0oaHR0cHM6Ly9kZXZlbG9wZXIubW96aWxsYS5vcmcvZW4tVVMvZG9jcy9XZWIvSmF2YVNjcmlwdC9SZWZlcmVuY2UvR2xvYmFsX09iamVjdHMvV2ViQXNzZW1ibHkvSW5zdGFuY2UpLlxuICAgICAqL1xuICAgIHJlYWRvbmx5IHdhc2lJbXBvcnQ6IFdlYkFzc2VtYmx5Lk1vZHVsZUltcG9ydHM7XG4gICAgcHJpdmF0ZSBpbnN0YW5jZTogV2ViQXNzZW1ibHkuSW5zdGFuY2UgfCBudWxsID0gbnVsbDtcbiAgICBwcml2YXRlIGlzU3RhcnRlZCA9IGZhbHNlO1xuICAgIHB1YmxpYyBhYmk6IFdBU0lBYmk7XG5cbiAgICBjb25zdHJ1Y3RvcihvcHRpb25zPzogV0FTSU9wdGlvbnMpIHtcbiAgICAgICAgdGhpcy53YXNpSW1wb3J0ID0ge307XG4gICAgICAgIHRoaXMuYWJpID0gbmV3IFdBU0lBYmkoKTtcbiAgICAgICAgaWYgKG9wdGlvbnM/LmZlYXR1cmVzKSB7XG4gICAgICAgICAgICBjb25zdCBpbXBvcnRQcm92aWRlcnM6IFJlY29yZDxzdHJpbmcsIHN0cmluZz4gPSB7fTtcbiAgICAgICAgICAgIGZvciAoY29uc3QgdXNlRmVhdHVyZSBvZiBvcHRpb25zLmZlYXR1cmVzKSB7XG4gICAgICAgICAgICAgICAgY29uc3QgZmVhdHVyZU5hbWUgPSB1c2VGZWF0dXJlLm5hbWUgfHwgJ1Vua25vd24gZmVhdHVyZSc7XG4gICAgICAgICAgICAgICAgY29uc3QgaW1wb3J0cyA9IHVzZUZlYXR1cmUob3B0aW9ucywgdGhpcy5hYmksIHRoaXMudmlldy5iaW5kKHRoaXMpKTtcbiAgICAgICAgICAgICAgICBmb3IgKGNvbnN0IGtleSBpbiBpbXBvcnRzKSB7XG4gICAgICAgICAgICAgICAgICAgIGlmIChrZXkgaW4gdGhpcy53YXNpSW1wb3J0KSB7XG4gICAgICAgICAgICAgICAgICAgICAgICBjb25zdCBwcmV2aW91c1Byb3ZpZGVyID0gaW1wb3J0UHJvdmlkZXJzW2tleV0gfHwgJ1Vua25vd24gZmVhdHVyZSc7XG4gICAgICAgICAgICAgICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoYEltcG9ydCBjb25mbGljdDogRnVuY3Rpb24gJyR7a2V5fScgaXMgYWxyZWFkeSBwcm92aWRlZCBieSAnJHtwcmV2aW91c1Byb3ZpZGVyfScgYW5kIGlzIGJlaW5nIHJlZGVmaW5lZCBieSAnJHtmZWF0dXJlTmFtZX0nYCk7XG4gICAgICAgICAgICAgICAgICAgIH1cbiAgICAgICAgICAgICAgICAgICAgaW1wb3J0UHJvdmlkZXJzW2tleV0gPSBmZWF0dXJlTmFtZTtcbiAgICAgICAgICAgICAgICB9XG4gICAgICAgICAgICAgICAgdGhpcy53YXNpSW1wb3J0ID0geyAuLi50aGlzLndhc2lJbXBvcnQsIC4uLmltcG9ydHMgfTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgfVxuICAgICAgICAvLyBQcm92aWRlIGRlZmF1bHQgaW1wbGVtZW50YXRpb25zIGZvciBtaXNzaW5nIGZ1bmN0aW9ucyBqdXN0IHJldHVybmluZyBFTk9TWVMuXG4gICAgICAgIGZvciAoY29uc3Qga2V5IG9mIFdBU0lBYmkuSU1QT1JUX0ZVTkNUSU9OUykge1xuICAgICAgICAgICAgaWYgKCEoa2V5IGluIHRoaXMud2FzaUltcG9ydCkpIHtcbiAgICAgICAgICAgICAgICB0aGlzLndhc2lJbXBvcnRba2V5XSA9ICgpID0+IHsgcmV0dXJuIFdBU0lBYmkuV0FTSV9FTk9TWVM7IH07XG4gICAgICAgICAgICB9XG4gICAgICAgIH1cbiAgICB9XG4gICAgXG5cbiAgICBnZXQgZXhwb3J0cygpOiBXZWJBc3NlbWJseS5FeHBvcnRzIHtcbiAgICAgICAgaWYgKCF0aGlzLmluc3RhbmNlKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoJ3dhc2kuc3RhcnQoKSBvciB3YXNpLmluaXRpYWxpemUoKSBoYXMgbm90IGJlZW4gY2FsbGVkJyk7XG4gICAgICAgIH1cbiAgICAgICAgcmV0dXJuIHRoaXMuaW5zdGFuY2UuZXhwb3J0cztcbiAgICB9XG5cblxuICAgIHByaXZhdGUgdmlldygpOiBEYXRhVmlldyB7XG4gICAgICAgIGlmICghdGhpcy5pbnN0YW5jZSkge1xuICAgICAgICAgICAgdGhyb3cgbmV3IEVycm9yKCd3YXNpLnN0YXJ0KCkgb3Igd2FzaS5pbml0aWFsaXplKCkgaGFzIG5vdCBiZWVuIGNhbGxlZCcpO1xuICAgICAgICB9XG4gICAgICAgIGlmICghdGhpcy5pbnN0YW5jZS5leHBvcnRzLm1lbW9yeSkge1xuICAgICAgICAgICAgdGhyb3cgbmV3IEVycm9yKCdpbnN0YW5jZS5leHBvcnRzLm1lbW9yeSBpcyB1bmRlZmluZWQnKTtcbiAgICAgICAgfVxuICAgICAgICBpZiAoISh0aGlzLmluc3RhbmNlLmV4cG9ydHMubWVtb3J5IGluc3RhbmNlb2YgV2ViQXNzZW1ibHkuTWVtb3J5KSkge1xuICAgICAgICAgICAgdGhyb3cgbmV3IEVycm9yKCdpbnN0YW5jZS5leHBvcnRzLm1lbW9yeSBpcyBub3QgYSBXZWJBc3NlbWJseS5NZW1vcnknKTtcbiAgICAgICAgfVxuICAgICAgICByZXR1cm4gbmV3IERhdGFWaWV3KHRoaXMuaW5zdGFuY2UuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKTtcbiAgICB9XG5cbiAgICAvKipcbiAgICogQXR0ZW1wdCB0byBpbml0aWFsaXplIGBpbnN0YW5jZWAgYXMgYSBXQVNJIHJlYWN0b3IgYnkgaW52b2tpbmcgaXRzYF9pbml0aWFsaXplKClgIGV4cG9ydCwgaWYgaXQgaXMgcHJlc2VudC4gSWYgYGluc3RhbmNlYCBjb250YWlucyBhIGBfc3RhcnQoKWBleHBvcnQsIHRoZW4gYW4gZXhjZXB0aW9uIGlzIHRocm93bi5cbiAgICpcbiAgICogYGluaXRpYWxpemUoKWAgcmVxdWlyZXMgdGhhdCBgaW5zdGFuY2VgIGV4cG9ydHMgYSBbYFdlYkFzc2VtYmx5Lk1lbW9yeWBdKGh0dHBzOi8vZGV2ZWxvcGVyLm1vemlsbGEub3JnL2VuLVVTL2RvY3MvV2ViL0phdmFTY3JpcHQvUmVmZXJlbmNlL0dsb2JhbF9PYmplY3RzL1dlYkFzc2VtYmx5L01lbW9yeSkgbmFtZWRgbWVtb3J5YC5cbiAgICogSWYgYGluc3RhbmNlYCBkb2VzIG5vdCBoYXZlIGEgYG1lbW9yeWAgZXhwb3J0IGFuIGV4Y2VwdGlvbiBpcyB0aHJvd24uXG4gICAqXG4gICAqIElmIGBpbml0aWFsaXplKClgIGlzIGNhbGxlZCBtb3JlIHRoYW4gb25jZSwgYW4gZXhjZXB0aW9uIGlzIHRocm93bi5cbiAgICovXG4gICAgYXN5bmMgaW5pdGlhbGl6ZShpbnN0YW5jZTogV2ViQXNzZW1ibHkuSW5zdGFuY2UpOiBQcm9taXNlPHZvaWQ+IHtcbiAgICAgICAgaWYgKHRoaXMuaXNTdGFydGVkKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoXG4gICAgICAgICAgICAgICAgXCJ3YXNpLnN0YXJ0KCkgb3Igd2FzaS5pbml0aWFsaXplKCkgaGFzIGFscmVhZHkgYmVlbiBjYWxsZWRcIixcbiAgICAgICAgICAgICk7XG4gICAgICAgIH1cbiAgICAgICAgdGhpcy5pc1N0YXJ0ZWQgPSB0cnVlO1xuICAgICAgICB0aGlzLmluc3RhbmNlID0gaW5zdGFuY2U7XG4gICAgICAgIGlmICghdGhpcy5pbnN0YW5jZS5leHBvcnRzLl9pbml0aWFsaXplKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoXCJpbnN0YW5jZS5leHBvcnRzLl9pbml0aWFsaXplIGlzIHVuZGVmaW5lZFwiKTtcbiAgICAgICAgfVxuICAgICAgICBpZiAodHlwZW9mIHRoaXMuaW5zdGFuY2UuZXhwb3J0cy5faW5pdGlhbGl6ZSAhPT0gXCJmdW5jdGlvblwiKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoXCJpbnN0YW5jZS5leHBvcnRzLl9pbml0aWFsaXplIGlzIG5vdCBhIGZ1bmN0aW9uXCIpO1xuICAgICAgICB9XG4gICAgICAgIGF3YWl0IHRoaXMuaW5zdGFuY2UuZXhwb3J0cy5faW5pdGlhbGl6ZSgpO1xuICAgIH1cblxuICAgIC8qKlxuICAgICAqIEF0dGVtcHQgdG8gYmVnaW4gZXhlY3V0aW9uIG9mIGBpbnN0YW5jZWAgYXMgYSBXQVNJIGNvbW1hbmQgYnkgaW52b2tpbmcgaXRzYF9zdGFydCgpYCBleHBvcnQuIElmIGBpbnN0YW5jZWAgZG9lcyBub3QgY29udGFpbiBhIGBfc3RhcnQoKWAgZXhwb3J0LCBvciBpZmBpbnN0YW5jZWAgY29udGFpbnMgYW4gYF9pbml0aWFsaXplKClgXG4gICAgICogZXhwb3J0LCB0aGVuIGFuIGV4Y2VwdGlvbiBpcyB0aHJvd24uXG4gICAgICpcbiAgICAgKiBgc3RhcnQoKWAgcmVxdWlyZXMgdGhhdCBgaW5zdGFuY2VgIGV4cG9ydHMgYSBbYFdlYkFzc2VtYmx5Lk1lbW9yeWBdKGh0dHBzOi8vZGV2ZWxvcGVyLm1vemlsbGEub3JnL2VuLVVTL2RvY3MvV2ViL0phdmFTY3JpcHQvUmVmZXJlbmNlL0dsb2JhbF9PYmplY3RzL1dlYkFzc2VtYmx5L01lbW9yeSkgbmFtZWRgbWVtb3J5YC4gSWZcbiAgICAgKiBgaW5zdGFuY2VgIGRvZXMgbm90IGhhdmUgYSBgbWVtb3J5YCBleHBvcnQgYW4gZXhjZXB0aW9uIGlzIHRocm93bi5cbiAgICAgKlxuICAgICAqIElmIGBzdGFydCgpYCBpcyBjYWxsZWQgbW9yZSB0aGFuIG9uY2UsIGFuIGV4Y2VwdGlvbiBpcyB0aHJvd24uXG4gICAgICovXG4gICAgYXN5bmMgc3RhcnQoaW5zdGFuY2U6IFdlYkFzc2VtYmx5Lkluc3RhbmNlKTogUHJvbWlzZTxudW1iZXI+IHtcbiAgICAgICAgaWYgKHRoaXMuaXNTdGFydGVkKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoJ3dhc2kuc3RhcnQoKSBvciB3YXNpLmluaXRpYWxpemUoKSBoYXMgYWxyZWFkeSBiZWVuIGNhbGxlZCcpO1xuICAgICAgICB9XG4gICAgICAgIHRoaXMuaXNTdGFydGVkID0gdHJ1ZTtcbiAgICAgICAgdGhpcy5pbnN0YW5jZSA9IGluc3RhbmNlO1xuICAgICAgICBpZiAoIXRoaXMuaW5zdGFuY2UuZXhwb3J0cy5fc3RhcnQpIHtcbiAgICAgICAgICAgIHRocm93IG5ldyBFcnJvcignaW5zdGFuY2UuZXhwb3J0cy5fc3RhcnQgaXMgdW5kZWZpbmVkJyk7XG4gICAgICAgIH1cbiAgICAgICAgaWYgKHR5cGVvZiB0aGlzLmluc3RhbmNlLmV4cG9ydHMuX3N0YXJ0ICE9PSAnZnVuY3Rpb24nKSB7XG4gICAgICAgICAgICB0aHJvdyBuZXcgRXJyb3IoJ2luc3RhbmNlLmV4cG9ydHMuX3N0YXJ0IGlzIG5vdCBhIGZ1bmN0aW9uJyk7XG4gICAgICAgIH1cbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIGF3YWl0IHRoaXMuaW5zdGFuY2UuZXhwb3J0cy5fc3RhcnQoKTtcbiAgICAgICAgICAgIHJldHVybiBXQVNJQWJpLldBU0lfRVNVQ0NFU1M7XG4gICAgICAgIH0gY2F0Y2ggKGUpIHtcbiAgICAgICAgICAgIGlmIChlIGluc3RhbmNlb2YgV0FTSVByb2NFeGl0KSB7XG4gICAgICAgICAgICAgICAgcmV0dXJuIGUuY29kZTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgICAgIHRocm93IGU7XG4gICAgICAgIH1cbiAgICB9XG59IiwKICAgICIvKipcbiAqIENvcHlyaWdodCAyMDE5IEdvb2dsZSBJbmMuIEFsbCBSaWdodHMgUmVzZXJ2ZWQuXG4gKlxuICogTGljZW5zZWQgdW5kZXIgdGhlIEFwYWNoZSBMaWNlbnNlLCBWZXJzaW9uIDIuMCAodGhlIFwiTGljZW5zZVwiKTtcbiAqIHlvdSBtYXkgbm90IHVzZSB0aGlzIGZpbGUgZXhjZXB0IGluIGNvbXBsaWFuY2Ugd2l0aCB0aGUgTGljZW5zZS5cbiAqIFlvdSBtYXkgb2J0YWluIGEgY29weSBvZiB0aGUgTGljZW5zZSBhdFxuICpcbiAqICAgICBodHRwOi8vd3d3LmFwYWNoZS5vcmcvbGljZW5zZXMvTElDRU5TRS0yLjBcbiAqXG4gKiBVbmxlc3MgcmVxdWlyZWQgYnkgYXBwbGljYWJsZSBsYXcgb3IgYWdyZWVkIHRvIGluIHdyaXRpbmcsIHNvZnR3YXJlXG4gKiBkaXN0cmlidXRlZCB1bmRlciB0aGUgTGljZW5zZSBpcyBkaXN0cmlidXRlZCBvbiBhbiBcIkFTIElTXCIgQkFTSVMsXG4gKiBXSVRIT1VUIFdBUlJBTlRJRVMgT1IgQ09ORElUSU9OUyBPRiBBTlkgS0lORCwgZWl0aGVyIGV4cHJlc3Mgb3IgaW1wbGllZC5cbiAqIFNlZSB0aGUgTGljZW5zZSBmb3IgdGhlIHNwZWNpZmljIGxhbmd1YWdlIGdvdmVybmluZyBwZXJtaXNzaW9ucyBhbmRcbiAqIGxpbWl0YXRpb25zIHVuZGVyIHRoZSBMaWNlbnNlLlxuICovXG5cbmNvbnN0IERBVEFfQUREUjogbnVtYmVyID0gMTY7XG5jb25zdCBEQVRBX1NUQVJUOiBudW1iZXIgPSBEQVRBX0FERFIgKyA4O1xuXG50eXBlIENhbGxhYmxlRm4gPSAoLi4uYXJnczogdW5rbm93bltdKSA9PiB1bmtub3duO1xuY29uc3QgV1JBUFBFRF9FWFBPUlRTOiBXZWFrTWFwPG9iamVjdCwgb2JqZWN0PiA9IG5ldyBXZWFrTWFwKCk7XG5cbi8vIERlZmF1bHQgZXhwb3J0cyB0aGF0IHNob3VsZCBuZXZlciBiZSB3cmFwcGVkXG5jb25zdCBERUZBVUxUX1VOV1JBUFBFRF9FWFBPUlRTID0gbmV3IFNldChbXCJmcmVlXCIsIFwibWFsbG9jXCJdKTtcblxuZW51bSBTdGF0ZSB7XG5cdE5vbmUgPSAwLFxuXHRVbndpbmRpbmcgPSAxLFxuXHRSZXdpbmRpbmcgPSAyLFxufVxuXG5pbnRlcmZhY2UgQXN5bmNpZnlFeHBvcnRzIGV4dGVuZHMgV2ViQXNzZW1ibHkuRXhwb3J0cyB7XG5cdGFzeW5jaWZ5X2dldF9zdGF0ZTogKCkgPT4gbnVtYmVyO1xuXHRhc3luY2lmeV9zdGFydF91bndpbmQ6IChhZGRyOiBudW1iZXIpID0+IHZvaWQ7XG5cdGFzeW5jaWZ5X3N0b3BfdW53aW5kOiAoKSA9PiB2b2lkO1xuXHRhc3luY2lmeV9zdGFydF9yZXdpbmQ6IChhZGRyOiBudW1iZXIpID0+IHZvaWQ7XG5cdGFzeW5jaWZ5X3N0b3BfcmV3aW5kOiAoKSA9PiB2b2lkO1xuXHRtZW1vcnk6IFdlYkFzc2VtYmx5Lk1lbW9yeTtcblx0X19zdGFja19wb2ludGVyOiBXZWJBc3NlbWJseS5HbG9iYWw7XG59XG5cbnR5cGUgSW1wb3J0Rm4gPSAoLi4uYXJnczogdW5rbm93bltdKSA9PiB1bmtub3duO1xudHlwZSBNb2R1bGVJbXBvcnRzID0gV2ViQXNzZW1ibHkuTW9kdWxlSW1wb3J0cztcbnR5cGUgSW1wb3J0cyA9IFdlYkFzc2VtYmx5LkltcG9ydHM7XG5cbi8qKlxuICogT3B0aW9ucyBmb3IgYXN5bmNpZnkgaW5zdGFudGlhdGlvbi5cbiAqL1xuZXhwb3J0IGludGVyZmFjZSBBc3luY2lmeU9wdGlvbnMge1xuXHQvKipcblx0ICogRXhwb3J0IG5hbWVzIHRoYXQgc2hvdWxkIG5vdCBiZSB3cmFwcGVkIHdpdGggYXN5bmMgaGFuZGxpbmcuXG5cdCAqIFVzZSB0aGlzIGZvciBzeW5jaHJvbm91cy1vbmx5IGZ1bmN0aW9ucyB0aGF0IG5ldmVyIHRyaWdnZXJcblx0ICogYXN5bmMgb3BlcmF0aW9ucyAobGlrZSBhc3luY2ptcF9ydF9zdGFydCkuXG5cdCAqL1xuXHR1bndyYXBwZWRFeHBvcnRzPzogc3RyaW5nW107XG59XG5cbmZ1bmN0aW9uIGlzUHJvbWlzZShvYmo6IHVua25vd24pOiBvYmogaXMgUHJvbWlzZTx1bmtub3duPiB7XG5cdHJldHVybiAoXG5cdFx0ISFvYmogJiZcblx0XHQodHlwZW9mIG9iaiA9PT0gXCJvYmplY3RcIiB8fCB0eXBlb2Ygb2JqID09PSBcImZ1bmN0aW9uXCIpICYmXG5cdFx0dHlwZW9mIChvYmogYXMgeyB0aGVuPzogdW5rbm93biB9KS50aGVuID09PSBcImZ1bmN0aW9uXCJcblx0KTtcbn1cblxuZnVuY3Rpb24gcHJveHlHZXQ8VCBleHRlbmRzIG9iamVjdD4ob2JqOiBULCB0cmFuc2Zvcm06ICh2YWx1ZTogdW5rbm93bikgPT4gdW5rbm93bik6IFQge1xuXHRyZXR1cm4gbmV3IFByb3h5KG9iaiwge1xuXHRcdGdldDogKG9iaiwgbmFtZTogc3RyaW5nIHwgc3ltYm9sKSA9PlxuXHRcdFx0dHJhbnNmb3JtKG9ialtuYW1lIGFzIGtleW9mIHR5cGVvZiBvYmpdKSxcblx0fSk7XG59XG5cbmNsYXNzIEFzeW5jaWZ5IHtcblx0cHJpdmF0ZSB2YWx1ZTogdW5rbm93biA9IHVuZGVmaW5lZDtcblx0cHJpdmF0ZSBleHBvcnRzOiBBc3luY2lmeUV4cG9ydHMgfCBudWxsID0gbnVsbDtcblx0cHJpdmF0ZSB1bndyYXBwZWRFeHBvcnRzOiBTZXQ8c3RyaW5nPjtcblxuXHRjb25zdHJ1Y3RvcihvcHRpb25zPzogQXN5bmNpZnlPcHRpb25zKSB7XG5cdFx0dGhpcy51bndyYXBwZWRFeHBvcnRzID0gbmV3IFNldChbXG5cdFx0XHQuLi5ERUZBVUxUX1VOV1JBUFBFRF9FWFBPUlRTLFxuXHRcdFx0Li4uKG9wdGlvbnM/LnVud3JhcHBlZEV4cG9ydHMgPz8gW10pLFxuXHRcdF0pO1xuXHR9XG5cblx0Z2V0U3RhdGUoKTogbnVtYmVyIHtcblx0XHRpZiAoIXRoaXMuZXhwb3J0cykgdGhyb3cgbmV3IEVycm9yKFwiRXhwb3J0cyBub3QgaW5pdGlhbGl6ZWRcIik7XG5cdFx0cmV0dXJuIHRoaXMuZXhwb3J0cy5hc3luY2lmeV9nZXRfc3RhdGUoKTtcblx0fVxuXG5cdGFzc2VydE5vbmVTdGF0ZSgpOiB2b2lkIHtcblx0XHRjb25zdCBzdGF0ZSA9IHRoaXMuZ2V0U3RhdGUoKTtcblx0XHRpZiAoc3RhdGUgIT09IFN0YXRlLk5vbmUpIHtcblx0XHRcdHRocm93IG5ldyBFcnJvcihgSW52YWxpZCBhc3luYyBzdGF0ZSAke3N0YXRlfSwgZXhwZWN0ZWQgMC5gKTtcblx0XHR9XG5cdH1cblxuXHR3cmFwSW1wb3J0Rm4oZm46IEltcG9ydEZuKTogSW1wb3J0Rm4ge1xuXHRcdHJldHVybiAoLi4uYXJnczogdW5rbm93bltdKSA9PiB7XG5cdFx0XHRpZiAodGhpcy5nZXRTdGF0ZSgpID09PSBTdGF0ZS5SZXdpbmRpbmcpIHtcblx0XHRcdFx0aWYgKCF0aGlzLmV4cG9ydHMpIHRocm93IG5ldyBFcnJvcihcIkV4cG9ydHMgbm90IGluaXRpYWxpemVkXCIpO1xuXHRcdFx0XHR0aGlzLmV4cG9ydHMuYXN5bmNpZnlfc3RvcF9yZXdpbmQoKTtcblx0XHRcdFx0cmV0dXJuIHRoaXMudmFsdWU7XG5cdFx0XHR9XG5cdFx0XHR0aGlzLmFzc2VydE5vbmVTdGF0ZSgpO1xuXHRcdFx0Y29uc3QgdmFsdWUgPSBmbiguLi5hcmdzKTtcblx0XHRcdGlmICghaXNQcm9taXNlKHZhbHVlKSkge1xuXHRcdFx0XHRyZXR1cm4gdmFsdWU7XG5cdFx0XHR9XG5cdFx0XHRpZiAoIXRoaXMuZXhwb3J0cykgdGhyb3cgbmV3IEVycm9yKFwiRXhwb3J0cyBub3QgaW5pdGlhbGl6ZWRcIik7XG5cdFx0XHR0aGlzLmV4cG9ydHMuYXN5bmNpZnlfc3RhcnRfdW53aW5kKERBVEFfQUREUik7XG5cdFx0XHR0aGlzLnZhbHVlID0gdmFsdWU7XG5cdFx0fTtcblx0fVxuXG5cdHdyYXBNb2R1bGVJbXBvcnRzKG1vZHVsZTogTW9kdWxlSW1wb3J0cyk6IE1vZHVsZUltcG9ydHMge1xuXHRcdHJldHVybiBwcm94eUdldChtb2R1bGUsICh2YWx1ZSkgPT4ge1xuXHRcdFx0aWYgKHR5cGVvZiB2YWx1ZSA9PT0gXCJmdW5jdGlvblwiKSB7XG5cdFx0XHRcdHJldHVybiB0aGlzLndyYXBJbXBvcnRGbih2YWx1ZSBhcyBJbXBvcnRGbik7XG5cdFx0XHR9XG5cdFx0XHRyZXR1cm4gdmFsdWU7XG5cdFx0fSkgYXMgTW9kdWxlSW1wb3J0cztcblx0fVxuXG5cdHdyYXBJbXBvcnRzKGltcG9ydHM/OiBJbXBvcnRzKTogSW1wb3J0cyB8IHVuZGVmaW5lZCB7XG5cdFx0aWYgKGltcG9ydHMgPT09IHVuZGVmaW5lZCkgcmV0dXJuO1xuXHRcdHJldHVybiBwcm94eUdldChpbXBvcnRzLCAobW9kdWxlSW1wb3J0cyA9IE9iamVjdC5jcmVhdGUobnVsbCkpID0+XG5cdFx0XHR0aGlzLndyYXBNb2R1bGVJbXBvcnRzKG1vZHVsZUltcG9ydHMgYXMgTW9kdWxlSW1wb3J0cyksXG5cdFx0KSBhcyBJbXBvcnRzO1xuXHR9XG5cblx0d3JhcEV4cG9ydEZuKGZuOiBDYWxsYWJsZUZuKTogQ2FsbGFibGVGbiB7XG5cdFx0bGV0IG5ld0V4cG9ydCA9IFdSQVBQRURfRVhQT1JUUy5nZXQoZm4pIGFzIENhbGxhYmxlRm4gfCB1bmRlZmluZWQ7XG5cdFx0aWYgKG5ld0V4cG9ydCAhPT0gdW5kZWZpbmVkKSB7XG5cdFx0XHRyZXR1cm4gbmV3RXhwb3J0O1xuXHRcdH1cblxuXHRcdG5ld0V4cG9ydCA9IGFzeW5jICguLi5hcmdzOiB1bmtub3duW10pID0+IHtcblx0XHRcdHRoaXMuYXNzZXJ0Tm9uZVN0YXRlKCk7XG5cdFx0XHRsZXQgcmVzdWx0ID0gZm4oLi4uYXJncyk7XG5cblx0XHRcdHdoaWxlICh0aGlzLmdldFN0YXRlKCkgPT09IFN0YXRlLlVud2luZGluZykge1xuXHRcdFx0XHRpZiAoIXRoaXMuZXhwb3J0cykgdGhyb3cgbmV3IEVycm9yKFwiRXhwb3J0cyBub3QgaW5pdGlhbGl6ZWRcIik7XG5cdFx0XHRcdHRoaXMuZXhwb3J0cy5hc3luY2lmeV9zdG9wX3Vud2luZCgpO1xuXHRcdFx0XHR0aGlzLnZhbHVlID0gYXdhaXQgKHRoaXMudmFsdWUgYXMgUHJvbWlzZTx1bmtub3duPik7XG5cdFx0XHRcdHRoaXMuYXNzZXJ0Tm9uZVN0YXRlKCk7XG5cdFx0XHRcdHRoaXMuZXhwb3J0cy5hc3luY2lmeV9zdGFydF9yZXdpbmQoREFUQV9BRERSKTtcblx0XHRcdFx0cmVzdWx0ID0gZm4oLi4uYXJncyk7XG5cdFx0XHR9XG5cblx0XHRcdHRoaXMuYXNzZXJ0Tm9uZVN0YXRlKCk7XG5cdFx0XHRyZXR1cm4gcmVzdWx0O1xuXHRcdH07XG5cblx0XHRXUkFQUEVEX0VYUE9SVFMuc2V0KGZuLCBuZXdFeHBvcnQpO1xuXHRcdHJldHVybiBuZXdFeHBvcnQ7XG5cdH1cblxuXHR3cmFwRXhwb3J0cyhleHBvcnRzOiBXZWJBc3NlbWJseS5FeHBvcnRzKTogV2ViQXNzZW1ibHkuRXhwb3J0cyB7XG5cdFx0Y29uc3QgbmV3RXhwb3J0cyA9IE9iamVjdC5jcmVhdGUobnVsbCk7XG5cblx0XHRmb3IgKGNvbnN0IGV4cG9ydE5hbWUgaW4gZXhwb3J0cykge1xuXHRcdFx0bGV0IHZhbHVlID0gZXhwb3J0c1tleHBvcnROYW1lXTtcblx0XHRcdGlmIChcblx0XHRcdFx0dHlwZW9mIHZhbHVlID09PSBcImZ1bmN0aW9uXCIgJiZcblx0XHRcdFx0IWV4cG9ydE5hbWUuc3RhcnRzV2l0aChcImFzeW5jaWZ5X1wiKSAmJlxuXHRcdFx0XHQhdGhpcy51bndyYXBwZWRFeHBvcnRzLmhhcyhleHBvcnROYW1lKVxuXHRcdFx0KSB7XG5cdFx0XHRcdHZhbHVlID0gdGhpcy53cmFwRXhwb3J0Rm4odmFsdWUgYXMgQ2FsbGFibGVGbik7XG5cdFx0XHR9XG5cdFx0XHRPYmplY3QuZGVmaW5lUHJvcGVydHkobmV3RXhwb3J0cywgZXhwb3J0TmFtZSwge1xuXHRcdFx0XHRlbnVtZXJhYmxlOiB0cnVlLFxuXHRcdFx0XHR2YWx1ZSxcblx0XHRcdH0pO1xuXHRcdH1cblxuXHRcdFdSQVBQRURfRVhQT1JUUy5zZXQoZXhwb3J0cywgbmV3RXhwb3J0cyk7XG5cdFx0cmV0dXJuIG5ld0V4cG9ydHM7XG5cdH1cblxuXHRpbml0KGluc3RhbmNlOiBXZWJBc3NlbWJseS5JbnN0YW5jZSwgaW1wb3J0cz86IEltcG9ydHMpOiB2b2lkIHtcblx0XHRjb25zdCBleHBvcnRzID0gaW5zdGFuY2UuZXhwb3J0cyBhcyBBc3luY2lmeUV4cG9ydHM7XG5cdFx0Y29uc3QgbWVtb3J5ID1cblx0XHRcdGV4cG9ydHMubWVtb3J5IHx8XG5cdFx0XHQoaW1wb3J0cz8uZW52ICYmIChpbXBvcnRzLmVudiBhcyB7IG1lbW9yeT86IFdlYkFzc2VtYmx5Lk1lbW9yeSB9KS5tZW1vcnkpO1xuXG5cdFx0aWYgKCFtZW1vcnkpIHtcblx0XHRcdHRocm93IG5ldyBFcnJvcihcIk1lbW9yeSBub3QgZm91bmQgaW4gZXhwb3J0cyBvciBpbXBvcnRzLmVudlwiKTtcblx0XHR9XG5cblx0XHRsZXQgZGF0YUVuZDogbnVtYmVyO1xuXHRcdGlmIChleHBvcnRzLl9fc3RhY2tfcG9pbnRlcikge1xuXHRcdFx0ZGF0YUVuZCA9IGV4cG9ydHMuX19zdGFja19wb2ludGVyLnZhbHVlIGFzIG51bWJlcjtcblx0XHR9IGVsc2Uge1xuXHRcdFx0ZGF0YUVuZCA9IDEwMjQ7XG5cdFx0fVxuXG5cdFx0bmV3IEludDMyQXJyYXkobWVtb3J5LmJ1ZmZlciwgREFUQV9BRERSKS5zZXQoW0RBVEFfU1RBUlQsIGRhdGFFbmRdKTtcblx0XHR0aGlzLmV4cG9ydHMgPSB0aGlzLndyYXBFeHBvcnRzKGV4cG9ydHMpIGFzIEFzeW5jaWZ5RXhwb3J0cztcblx0XHRPYmplY3Quc2V0UHJvdG90eXBlT2YoaW5zdGFuY2UsIEluc3RhbmNlLnByb3RvdHlwZSk7XG5cdH1cbn1cblxuZXhwb3J0IGNsYXNzIEluc3RhbmNlIGV4dGVuZHMgV2ViQXNzZW1ibHkuSW5zdGFuY2Uge1xuXHRjb25zdHJ1Y3Rvcihcblx0XHRtb2R1bGU6IFdlYkFzc2VtYmx5Lk1vZHVsZSxcblx0XHRpbXBvcnRzPzogSW1wb3J0cyxcblx0XHRvcHRpb25zPzogQXN5bmNpZnlPcHRpb25zLFxuXHQpIHtcblx0XHRjb25zdCBzdGF0ZSA9IG5ldyBBc3luY2lmeShvcHRpb25zKTtcblx0XHRzdXBlcihtb2R1bGUsIHN0YXRlLndyYXBJbXBvcnRzKGltcG9ydHMpKTtcblx0XHRzdGF0ZS5pbml0KHRoaXMsIGltcG9ydHMpO1xuXHR9XG5cblx0b3ZlcnJpZGUgZ2V0IGV4cG9ydHMoKTogV2ViQXNzZW1ibHkuRXhwb3J0cyB7XG5cdFx0cmV0dXJuIFdSQVBQRURfRVhQT1JUUy5nZXQoc3VwZXIuZXhwb3J0cykgYXMgV2ViQXNzZW1ibHkuRXhwb3J0cztcblx0fVxufVxuXG5PYmplY3QuZGVmaW5lUHJvcGVydHkoSW5zdGFuY2UucHJvdG90eXBlLCBcImV4cG9ydHNcIiwgeyBlbnVtZXJhYmxlOiB0cnVlIH0pO1xuXG4vKipcbiAqIEluc3RhbnRpYXRlIGEgV2ViQXNzZW1ibHkgbW9kdWxlIHdpdGggYXN5bmNpZnkgc3VwcG9ydC5cbiAqXG4gKiBAcGFyYW0gc291cmNlIC0gVGhlIFdlYkFzc2VtYmx5IGJpbmFyeVxuICogQHBhcmFtIGltcG9ydHMgLSBJbXBvcnQgb2JqZWN0IGZvciB0aGUgbW9kdWxlXG4gKiBAcGFyYW0gb3B0aW9ucyAtIEFzeW5jaWZ5IG9wdGlvbnMgaW5jbHVkaW5nIHdoaWNoIGV4cG9ydHMgdG8gc2tpcCB3cmFwcGluZ1xuICovXG5leHBvcnQgYXN5bmMgZnVuY3Rpb24gaW5zdGFudGlhdGUoXG5cdHNvdXJjZTogQXJyYXlCdWZmZXJMaWtlLFxuXHRpbXBvcnRzPzogSW1wb3J0cyxcblx0b3B0aW9ucz86IEFzeW5jaWZ5T3B0aW9ucyxcbik6IFByb21pc2U8V2ViQXNzZW1ibHkuV2ViQXNzZW1ibHlJbnN0YW50aWF0ZWRTb3VyY2U+IHtcblx0Y29uc3Qgc3RhdGUgPSBuZXcgQXN5bmNpZnkob3B0aW9ucyk7XG5cdGNvbnN0IHJlc3VsdCA9IGF3YWl0IFdlYkFzc2VtYmx5Lmluc3RhbnRpYXRlKFxuXHRcdHNvdXJjZSxcblx0XHRzdGF0ZS53cmFwSW1wb3J0cyhpbXBvcnRzKSxcblx0KTtcblx0c3RhdGUuaW5pdChyZXN1bHQuaW5zdGFuY2UsIGltcG9ydHMpO1xuXHRyZXR1cm4gcmVzdWx0O1xufVxuXG4vKipcbiAqIEluc3RhbnRpYXRlIGEgV2ViQXNzZW1ibHkgbW9kdWxlIGZyb20gYSBzdHJlYW1pbmcgc291cmNlIHdpdGggYXN5bmNpZnkgc3VwcG9ydC5cbiAqXG4gKiBAcGFyYW0gc291cmNlIC0gUmVzcG9uc2Ugb3IgUHJvbWlzZSBvZiBSZXNwb25zZSBjb250YWluaW5nIHRoZSBXQVNNXG4gKiBAcGFyYW0gaW1wb3J0cyAtIEltcG9ydCBvYmplY3QgZm9yIHRoZSBtb2R1bGVcbiAqIEBwYXJhbSBvcHRpb25zIC0gQXN5bmNpZnkgb3B0aW9ucyBpbmNsdWRpbmcgd2hpY2ggZXhwb3J0cyB0byBza2lwIHdyYXBwaW5nXG4gKi9cbmV4cG9ydCBhc3luYyBmdW5jdGlvbiBpbnN0YW50aWF0ZVN0cmVhbWluZyhcblx0c291cmNlOiBSZXNwb25zZSB8IFByb21pc2U8UmVzcG9uc2U+LFxuXHRpbXBvcnRzPzogSW1wb3J0cyxcblx0b3B0aW9ucz86IEFzeW5jaWZ5T3B0aW9ucyxcbik6IFByb21pc2U8V2ViQXNzZW1ibHkuV2ViQXNzZW1ibHlJbnN0YW50aWF0ZWRTb3VyY2U+IHtcblx0Y29uc3Qgc3RhdGUgPSBuZXcgQXN5bmNpZnkob3B0aW9ucyk7XG5cdGNvbnN0IHJlc3VsdCA9IGF3YWl0IFdlYkFzc2VtYmx5Lmluc3RhbnRpYXRlU3RyZWFtaW5nKFxuXHRcdHNvdXJjZSxcblx0XHRzdGF0ZS53cmFwSW1wb3J0cyhpbXBvcnRzKSxcblx0KTtcblx0c3RhdGUuaW5pdChyZXN1bHQuaW5zdGFuY2UsIGltcG9ydHMpO1xuXHRyZXR1cm4gcmVzdWx0O1xufSIsCiAgICAiaW1wb3J0IHtcbiAgICB1c2VBcmdzLFxuICAgIHVzZUNsb2NrLFxuICAgIHVzZUVudmlyb24sXG4gICAgdXNlTWVtb3J5RlMsXG4gICAgdXNlUHJvYyxcbiAgICB1c2VSYW5kb20sXG4gICAgV0FTSSxcbiAgICBXQVNJUHJvY0V4aXQsXG59IGZyb20gXCIuL3dhc2lcIjtcbmltcG9ydCB7IGluc3RhbnRpYXRlIH0gZnJvbSBcIi4vd2FzaS9hc3luY2lmeVwiO1xuaW1wb3J0IHsgTWVtb3J5RmlsZVN5c3RlbSB9IGZyb20gXCIuL3dhc2kvZmVhdHVyZXMvZmRcIjtcbmltcG9ydCB0eXBlIHsgV0FTSU9wdGlvbnMgfSBmcm9tIFwiLi93YXNpL29wdGlvbnNcIjtcbmltcG9ydCB6ZXJvcGVybCBmcm9tIFwiLi96ZXJvcGVybC53YXNtXCI7XG5cbmV4cG9ydCB7IE1lbW9yeUZpbGVTeXN0ZW0gfSBmcm9tIFwiLi93YXNpL2ZlYXR1cmVzL2ZkXCI7XG5cbi8qKlxuICogQGZpbGVvdmVydmlldyB6ZXJvcGVybC10cy5cbiAqXG4gKiBQcm92aWRlcyBhIEphdmFTY3JpcHQgaW50ZXJmYWNlIHRvIGEgUGVybCBpbnRlcnByZXRlciBydW5uaW5nIGluIFdlYkFzc2VtYmx5LlxuICogU3VwcG9ydHMgUGVybCB2YWx1ZSBtYW5pcHVsYXRpb24sIGFycmF5cywgaGFzaGVzLCByZWZlcmVuY2VzLCBhbmQgYmlkaXJlY3Rpb25hbFxuICogZnVuY3Rpb24gY2FsbHMgYmV0d2VlbiBKYXZhU2NyaXB0IGFuZCBQZXJsLlxuICogXG4gKiBAZXhhbXBsZVxuICogQmFzaWMgdXNhZ2U6XG4gKiBgYGB0eXBlc2NyaXB0XG4gKiBpbXBvcnQgeyBaZXJvUGVybCB9IGZyb20gXCJAY29sb3JoeXRobS96ZXJvcGVybC10c1wiO1xuICpcbiAqIGNvbnN0IHBlcmwgPSBhd2FpdCBaZXJvUGVybC5jcmVhdGUoKTtcbiAqIGF3YWl0IHBlcmwuZXZhbCgncHJpbnQgXCJIZWxsbywgV29ybGQhXFxuXCInKTtcbiAqIHBlcmwuZGlzcG9zZSgpO1xuICogYGBgXG4gKlxuICogQGV4YW1wbGVcbiAqIFdvcmtpbmcgd2l0aCBkYXRhIHN0cnVjdHVyZXM6XG4gKiBgYGB0eXBlc2NyaXB0XG4gKiBjb25zdCBwZXJsID0gYXdhaXQgWmVyb1BlcmwuY3JlYXRlKCk7XG4gKlxuICogLy8gQ3JlYXRlIGhhc2hcbiAqIGNvbnN0IGhhc2ggPSBwZXJsLmNyZWF0ZUhhc2goe1xuICogICBuYW1lOiAnQWxpY2UnLFxuICogICBhZ2U6IDMwLFxuICogICBhY3RpdmU6IHRydWVcbiAqIH0pO1xuICpcbiAqIC8vIENyZWF0ZSBhcnJheVxuICogY29uc3QgYXJyID0gcGVybC5jcmVhdGVBcnJheShbMSwgMiwgMywgXCJoZWxsb1wiXSk7XG4gKlxuICogLy8gQ29udmVydCB0byBKYXZhU2NyaXB0XG4gKiBjb25zdCBvYmogPSBoYXNoLnByb2plY3QoKTsgLy8geyBuYW1lOiAnQWxpY2UnLCBhZ2U6IDMwLCBhY3RpdmU6IHRydWUgfVxuICogY29uc3QganNBcnIgPSBhcnIucHJvamVjdCgpOyAvLyBbMSwgMiwgMywgXCJoZWxsb1wiXVxuICpcbiAqIGhhc2guZGlzcG9zZSgpO1xuICogYXJyLmRpc3Bvc2UoKTtcbiAqIHBlcmwuZGlzcG9zZSgpO1xuICogYGBgXG4gKlxuICogQGV4YW1wbGVcbiAqIENhbGxpbmcgSmF2YVNjcmlwdCBmcm9tIFBlcmw6XG4gKiBgYGB0eXBlc2NyaXB0XG4gKiBjb25zdCBwZXJsID0gYXdhaXQgWmVyb1BlcmwuY3JlYXRlKCk7XG4gKlxuICogcGVybC5yZWdpc3RlckZ1bmN0aW9uKCdncmVldCcsIChuYW1lKSA9PiB7XG4gKiAgIGNvbnN0IG5hbWVTdHIgPSBuYW1lLnRvU3RyaW5nKCk7XG4gKiAgIGNvbnNvbGUubG9nKGBIZWxsbywgJHtuYW1lU3RyfSFgKTtcbiAqICAgcmV0dXJuIHBlcmwuY3JlYXRlU3RyaW5nKGBHcmVldGVkICR7bmFtZVN0cn1gKTtcbiAqIH0pO1xuICpcbiAqIGF3YWl0IHBlcmwuZXZhbCgnZ3JlZXQoXCJBbGljZVwiKScpO1xuICogcGVybC5kaXNwb3NlKCk7XG4gKiBgYGBcbiAqL1xuXG4vKiogUGVybCB2YWx1ZSB0eXBlcy4gKi9cbmV4cG9ydCB0eXBlIFBlcmxWYWx1ZVR5cGUgPVxuICAgIHwgXCJ1bmRlZlwiIHwgXCJ0cnVlXCIgfCBcImZhbHNlXCIgfCBcImludFwiIHwgXCJkb3VibGVcIlxuICAgIHwgXCJzdHJpbmdcIiB8IFwiYXJyYXlcIiB8IFwiaGFzaFwiIHwgXCJjb2RlXCIgfCBcInJlZlwiO1xuXG4vKiogUGVybCBjYWxsaW5nIGNvbnRleHQuICovXG5leHBvcnQgdHlwZSBQZXJsQ29udGV4dCA9IFwidm9pZFwiIHwgXCJzY2FsYXJcIiB8IFwibGlzdFwiO1xuXG4vKiogSmF2YVNjcmlwdCB2YWx1ZXMgdGhhdCBjYW4gYmUgY29udmVydGVkIHRvIFBlcmwgdmFsdWVzLiAqL1xuZXhwb3J0IHR5cGUgUGVybENvbnZlcnRpYmxlID1cbiAgICB8IFBlcmxWYWx1ZSB8IHN0cmluZyB8IG51bWJlciB8IGJvb2xlYW4gfCBudWxsIHwgdW5kZWZpbmVkXG4gICAgfCBQZXJsQ29udmVydGlibGVbXSB8IHsgW2tleTogc3RyaW5nXTogUGVybENvbnZlcnRpYmxlIH07XG5cbi8qKiBKYXZhU2NyaXB0IHByaW1pdGl2ZSB0eXBlcyB0aGF0IFBlcmwgdmFsdWVzIGNhbiBiZSBjb252ZXJ0ZWQgdG8uICovXG5leHBvcnQgdHlwZSBKU1ByaW1pdGl2ZSA9IHN0cmluZyB8IG51bWJlciB8IGJvb2xlYW4gfCBudWxsIHwgdW5kZWZpbmVkO1xuXG4vLyBTeW5jaHJvbm91cyBleHBvcnRzIChkb24ndCB0cmlnZ2VyIGFzeW5jam1wX3J0X3N0YXJ0KVxuaW50ZXJmYWNlIFplcm9QZXJsU3luY0V4cG9ydHMge1xuICAgIG1lbW9yeTogV2ViQXNzZW1ibHkuTWVtb3J5O1xuICAgIG1hbGxvYzogKHNpemU6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIGZyZWU6IChwdHI6IG51bWJlcikgPT4gdm9pZDtcblxuICAgIHplcm9wZXJsX2ZyZWVfaW50ZXJwcmV0ZXI6ICgpID0+IHZvaWQ7XG4gICAgemVyb3Blcmxfc2h1dGRvd246ICgpID0+IHZvaWQ7XG4gICAgemVyb3BlcmxfbGFzdF9lcnJvcjogKCkgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2NsZWFyX2Vycm9yOiAoKSA9PiB2b2lkO1xuICAgIHplcm9wZXJsX2lzX2luaXRpYWxpemVkOiAoKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfY2FuX2V2YWx1YXRlOiAoKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfZmx1c2g6ICgpID0+IG51bWJlcjtcblxuICAgIHplcm9wZXJsX25ld19pbnQ6IChpOiBudW1iZXIpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9uZXdfdWludDogKHU6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX25ld19kb3VibGU6IChkOiBudW1iZXIpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9uZXdfc3RyaW5nOiAocHRyOiBudW1iZXIsIGxlbjogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfbmV3X2Jvb2w6IChiOiBudW1iZXIpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9uZXdfdW5kZWY6ICgpID0+IG51bWJlcjtcblxuICAgIHplcm9wZXJsX3RvX2ludDogKHZhbDogbnVtYmVyLCBvdXQ6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX3RvX2RvdWJsZTogKHZhbDogbnVtYmVyLCBvdXQ6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX3RvX3N0cmluZzogKHZhbDogbnVtYmVyLCBsZW46IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX3RvX2Jvb2w6ICh2YWw6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2lzX3VuZGVmOiAodmFsOiBudW1iZXIpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9nZXRfdHlwZTogKHZhbDogbnVtYmVyKSA9PiBudW1iZXI7XG5cbiAgICB6ZXJvcGVybF9pbmNyZWY6ICh2YWw6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF9kZWNyZWY6ICh2YWw6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF92YWx1ZV9mcmVlOiAodmFsOiBudW1iZXIpID0+IHZvaWQ7XG5cbiAgICB6ZXJvcGVybF9uZXdfYXJyYXk6ICgpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9hcnJheV9wdXNoOiAoYXJyOiBudW1iZXIsIHZhbDogbnVtYmVyKSA9PiB2b2lkO1xuICAgIHplcm9wZXJsX2FycmF5X3BvcDogKGFycjogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfYXJyYXlfZ2V0OiAoYXJyOiBudW1iZXIsIGlkeDogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfYXJyYXlfc2V0OiAoYXJyOiBudW1iZXIsIGlkeDogbnVtYmVyLCB2YWw6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2FycmF5X2xlbmd0aDogKGFycjogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfYXJyYXlfY2xlYXI6IChhcnI6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF9hcnJheV90b192YWx1ZTogKGFycjogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfdmFsdWVfdG9fYXJyYXk6ICh2YWw6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2FycmF5X2ZyZWU6IChhcnI6IG51bWJlcikgPT4gdm9pZDtcblxuICAgIHplcm9wZXJsX25ld19oYXNoOiAoKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfaGFzaF9zZXQ6IChoOiBudW1iZXIsIGs6IG51bWJlciwgdjogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfaGFzaF9nZXQ6IChoOiBudW1iZXIsIGs6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2hhc2hfZXhpc3RzOiAoaDogbnVtYmVyLCBrOiBudW1iZXIpID0+IG51bWJlcjtcbiAgICB6ZXJvcGVybF9oYXNoX2RlbGV0ZTogKGg6IG51bWJlciwgazogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfaGFzaF9jbGVhcjogKGg6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF9oYXNoX2l0ZXJfbmV3OiAoaDogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfaGFzaF9pdGVyX25leHQ6IChpdDogbnVtYmVyLCBrOiBudW1iZXIsIHY6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2hhc2hfaXRlcl9mcmVlOiAoaXQ6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF9oYXNoX3RvX3ZhbHVlOiAoaDogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfdmFsdWVfdG9faGFzaDogKHZhbDogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfaGFzaF9mcmVlOiAoaDogbnVtYmVyKSA9PiB2b2lkO1xuXG4gICAgemVyb3BlcmxfbmV3X3JlZjogKHZhbDogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfZGVyZWY6IChyZWY6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2lzX3JlZjogKHZhbDogbnVtYmVyKSA9PiBudW1iZXI7XG5cbiAgICB6ZXJvcGVybF9nZXRfdmFyOiAobmFtZTogbnVtYmVyKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfZ2V0X2FycmF5X3ZhcjogKG5hbWU6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX2dldF9oYXNoX3ZhcjogKG5hbWU6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX3NldF92YXI6IChuYW1lOiBudW1iZXIsIHZhbDogbnVtYmVyKSA9PiBudW1iZXI7XG5cbiAgICB6ZXJvcGVybF9yZWdpc3Rlcl9mdW5jdGlvbjogKGlkOiBudW1iZXIsIG5hbWU6IG51bWJlcikgPT4gdm9pZDtcbiAgICB6ZXJvcGVybF9yZWdpc3Rlcl9tZXRob2Q6IChpZDogbnVtYmVyLCBwa2c6IG51bWJlciwgbWV0aDogbnVtYmVyKSA9PiB2b2lkO1xuXG4gICAgemVyb3BlcmxfcmVzdWx0X2dldDogKHJlczogbnVtYmVyLCBpZHg6IG51bWJlcikgPT4gbnVtYmVyO1xuICAgIHplcm9wZXJsX3Jlc3VsdF9mcmVlOiAocmVzOiBudW1iZXIpID0+IHZvaWQ7XG5cbiAgICB6ZXJvcGVybF9zZXRfaG9zdF9lcnJvcjogKGVycjogbnVtYmVyKSA9PiB2b2lkO1xuICAgIHplcm9wZXJsX2dldF9ob3N0X2Vycm9yOiAoKSA9PiBudW1iZXI7XG4gICAgemVyb3BlcmxfY2xlYXJfaG9zdF9lcnJvcjogKCkgPT4gdm9pZDtcbn1cblxuLy8gQXN5bmMgZXhwb3J0cyAodHJpZ2dlciBhc3luY2ptcF9ydF9zdGFydClcbmludGVyZmFjZSBaZXJvUGVybEFzeW5jRXhwb3J0cyB7XG4gICAgemVyb3BlcmxfaW5pdDogKCkgPT4gUHJvbWlzZTxudW1iZXI+O1xuICAgIHplcm9wZXJsX2luaXRfd2l0aF9hcmdzOiAoYXJnYzogbnVtYmVyLCBhcmd2OiBudW1iZXIpID0+IFByb21pc2U8bnVtYmVyPjtcbiAgICB6ZXJvcGVybF9yZXNldDogKCkgPT4gUHJvbWlzZTxudW1iZXI+O1xuICAgIHplcm9wZXJsX2V2YWw6IChjb2RlOiBudW1iZXIsIGN0eDogbnVtYmVyLCBhcmdjOiBudW1iZXIsIGFyZ3Y6IG51bWJlcikgPT4gUHJvbWlzZTxudW1iZXI+O1xuICAgIHplcm9wZXJsX3J1bl9maWxlOiAocGF0aDogbnVtYmVyLCBhcmdjOiBudW1iZXIsIGFyZ3Y6IG51bWJlcikgPT4gUHJvbWlzZTxudW1iZXI+O1xuICAgIHplcm9wZXJsX2NhbGw6IChuYW1lOiBudW1iZXIsIGN0eDogbnVtYmVyLCBhcmdjOiBudW1iZXIsIGFyZ3Y6IG51bWJlcikgPT4gUHJvbWlzZTxudW1iZXI+O1xufVxuXG50eXBlIFplcm9QZXJsRXhwb3J0cyA9IFplcm9QZXJsU3luY0V4cG9ydHMgJiBaZXJvUGVybEFzeW5jRXhwb3J0cyAmIFdlYkFzc2VtYmx5LkV4cG9ydHM7XG5cbi8vIFN5bmNocm9ub3VzIGV4cG9ydHMgdGhhdCBzaG91bGQgbm90IGJlIHdyYXBwZWQgYnkgYXN5bmNpZnlcbmNvbnN0IFNZTkNfRVhQT1JUUzogc3RyaW5nW10gPSBbXG4gICAgXCJ6ZXJvcGVybF9mcmVlX2ludGVycHJldGVyXCIsIFwiemVyb3Blcmxfc2h1dGRvd25cIiwgXCJ6ZXJvcGVybF9sYXN0X2Vycm9yXCIsXG4gICAgXCJ6ZXJvcGVybF9jbGVhcl9lcnJvclwiLCBcInplcm9wZXJsX2lzX2luaXRpYWxpemVkXCIsIFwiemVyb3BlcmxfY2FuX2V2YWx1YXRlXCIsXG4gICAgXCJ6ZXJvcGVybF9mbHVzaFwiLCBcInplcm9wZXJsX25ld19pbnRcIiwgXCJ6ZXJvcGVybF9uZXdfdWludFwiLCBcInplcm9wZXJsX25ld19kb3VibGVcIixcbiAgICBcInplcm9wZXJsX25ld19zdHJpbmdcIiwgXCJ6ZXJvcGVybF9uZXdfYm9vbFwiLCBcInplcm9wZXJsX25ld191bmRlZlwiLFxuICAgIFwiemVyb3BlcmxfdG9faW50XCIsIFwiemVyb3BlcmxfdG9fZG91YmxlXCIsIFwiemVyb3BlcmxfdG9fc3RyaW5nXCIsIFwiemVyb3BlcmxfdG9fYm9vbFwiLFxuICAgIFwiemVyb3BlcmxfaXNfdW5kZWZcIiwgXCJ6ZXJvcGVybF9nZXRfdHlwZVwiLCBcInplcm9wZXJsX2luY3JlZlwiLCBcInplcm9wZXJsX2RlY3JlZlwiLFxuICAgIFwiemVyb3BlcmxfdmFsdWVfZnJlZVwiLCBcInplcm9wZXJsX25ld19hcnJheVwiLCBcInplcm9wZXJsX2FycmF5X3B1c2hcIixcbiAgICBcInplcm9wZXJsX2FycmF5X3BvcFwiLCBcInplcm9wZXJsX2FycmF5X2dldFwiLCBcInplcm9wZXJsX2FycmF5X3NldFwiLFxuICAgIFwiemVyb3BlcmxfYXJyYXlfbGVuZ3RoXCIsIFwiemVyb3BlcmxfYXJyYXlfY2xlYXJcIiwgXCJ6ZXJvcGVybF9hcnJheV90b192YWx1ZVwiLFxuICAgIFwiemVyb3BlcmxfdmFsdWVfdG9fYXJyYXlcIiwgXCJ6ZXJvcGVybF9hcnJheV9mcmVlXCIsIFwiemVyb3BlcmxfbmV3X2hhc2hcIixcbiAgICBcInplcm9wZXJsX2hhc2hfc2V0XCIsIFwiemVyb3BlcmxfaGFzaF9nZXRcIiwgXCJ6ZXJvcGVybF9oYXNoX2V4aXN0c1wiLFxuICAgIFwiemVyb3BlcmxfaGFzaF9kZWxldGVcIiwgXCJ6ZXJvcGVybF9oYXNoX2NsZWFyXCIsIFwiemVyb3BlcmxfaGFzaF9pdGVyX25ld1wiLFxuICAgIFwiemVyb3BlcmxfaGFzaF9pdGVyX25leHRcIiwgXCJ6ZXJvcGVybF9oYXNoX2l0ZXJfZnJlZVwiLCBcInplcm9wZXJsX2hhc2hfdG9fdmFsdWVcIixcbiAgICBcInplcm9wZXJsX3ZhbHVlX3RvX2hhc2hcIiwgXCJ6ZXJvcGVybF9oYXNoX2ZyZWVcIiwgXCJ6ZXJvcGVybF9uZXdfcmVmXCIsXG4gICAgXCJ6ZXJvcGVybF9kZXJlZlwiLCBcInplcm9wZXJsX2lzX3JlZlwiLCBcInplcm9wZXJsX2dldF92YXJcIiwgXCJ6ZXJvcGVybF9nZXRfYXJyYXlfdmFyXCIsXG4gICAgXCJ6ZXJvcGVybF9nZXRfaGFzaF92YXJcIiwgXCJ6ZXJvcGVybF9zZXRfdmFyXCIsIFwiemVyb3BlcmxfcmVnaXN0ZXJfZnVuY3Rpb25cIixcbiAgICBcInplcm9wZXJsX3JlZ2lzdGVyX21ldGhvZFwiLCBcInplcm9wZXJsX3Jlc3VsdF9nZXRcIiwgXCJ6ZXJvcGVybF9yZXN1bHRfZnJlZVwiLFxuICAgIFwiemVyb3Blcmxfc2V0X2hvc3RfZXJyb3JcIiwgXCJ6ZXJvcGVybF9nZXRfaG9zdF9lcnJvclwiLCBcInplcm9wZXJsX2NsZWFyX2hvc3RfZXJyb3JcIixcbl07XG5cbnR5cGUgRmV0Y2hMaWtlID0gKC4uLmFyZ3M6IHVua25vd25bXSkgPT4gUHJvbWlzZTxSZXNwb25zZT47XG5cbi8qKlxuICogRnVuY3Rpb24gdHlwZSB0aGF0IGNhbiBiZSByZWdpc3RlcmVkIGFzIGEgUGVybCBmdW5jdGlvbi5cbiAqIFJlY2VpdmVzIFBlcmwgdmFsdWVzIGFzIGFyZ3VtZW50cyBhbmQgcmV0dXJucyBhIFBlcmwgdmFsdWUgb3Igdm9pZC5cbiAqIENhbiBiZSBzeW5jIG9yIGFzeW5jLlxuICovXG5leHBvcnQgdHlwZSBIb3N0RnVuY3Rpb24gPSAoXG4gICAgLi4uYXJnczogUGVybFZhbHVlW11cbikgPT4gUGVybFZhbHVlIHwgUHJvbWlzZTxQZXJsVmFsdWU+IHwgdm9pZCB8IFByb21pc2U8dm9pZD47XG5cbi8qKiBFcnJvciBjbGFzcyBmb3IgWmVyb1Blcmwgb3BlcmF0aW9ucy4gKi9cbmV4cG9ydCBjbGFzcyBaZXJvUGVybEVycm9yIGV4dGVuZHMgRXJyb3Ige1xuICAgIHJlYWRvbmx5IGV4aXRDb2RlPzogbnVtYmVyO1xuICAgIHJlYWRvbmx5IHBlcmxFcnJvcj86IHN0cmluZztcblxuICAgIGNvbnN0cnVjdG9yKG1lc3NhZ2U6IHN0cmluZywgZXhpdENvZGU/OiBudW1iZXIsIHBlcmxFcnJvcj86IHN0cmluZykge1xuICAgICAgICBzdXBlcihtZXNzYWdlKTtcbiAgICAgICAgdGhpcy5uYW1lID0gXCJaZXJvUGVybEVycm9yXCI7XG4gICAgICAgIHRoaXMuZXhpdENvZGUgPSBleGl0Q29kZTtcbiAgICAgICAgdGhpcy5wZXJsRXJyb3IgPSBwZXJsRXJyb3I7XG4gICAgICAgIGlmIChFcnJvci5jYXB0dXJlU3RhY2tUcmFjZSkgRXJyb3IuY2FwdHVyZVN0YWNrVHJhY2UodGhpcywgWmVyb1BlcmxFcnJvcik7XG4gICAgfVxufVxuXG4vKiogT3B0aW9ucyBmb3IgY3JlYXRpbmcgYSBaZXJvUGVybCBpbnN0YW5jZS4gKi9cbmV4cG9ydCBpbnRlcmZhY2UgWmVyb1BlcmxPcHRpb25zIHtcbiAgICBlbnY/OiBSZWNvcmQ8c3RyaW5nLCBzdHJpbmc+O1xuICAgIGZpbGVTeXN0ZW0/OiBNZW1vcnlGaWxlU3lzdGVtO1xuICAgIHN0ZG91dD86IChkYXRhOiBzdHJpbmcgfCBVaW50OEFycmF5KSA9PiB2b2lkO1xuICAgIHN0ZGVycj86IChkYXRhOiBzdHJpbmcgfCBVaW50OEFycmF5KSA9PiB2b2lkO1xuICAgIGZldGNoPzogRmV0Y2hMaWtlO1xuICAgIC8qKlxuICAgICAqIFdoZW4gdHJ1ZSwgc3Rkb3V0L3N0ZGVyciBoYW5kbGVycyByZWNlaXZlIHJhdyBVaW50OEFycmF5IGNodW5rc1xuICAgICAqIGluc3RlYWQgb2YgcGVyLWNodW5rIFVURi04LWRlY29kZWQgc3RyaW5ncy4gUmVxdWlyZWQgZm9yIGJpbmFyeVxuICAgICAqIG91dHB1dCAoZS5nLiBleGlmdG9vbCAtYikg4oCUIHBlci1jaHVuayB0ZXh0IGRlY29kaW5nIGNvcnJ1cHRzXG4gICAgICogbm9uLVVURi04IGJ5dGVzIGlycmV2ZXJzaWJseSAoVStGRkZEIHJlcGxhY2VtZW50IGNoYXJhY3RlcnMpLlxuICAgICAqL1xuICAgIG91dHB1dEJ1ZmZlcnM/OiBib29sZWFuO1xufVxuXG4vKiogUmVzdWx0IG9mIGEgUGVybCBldmFsdWF0aW9uIG9yIGZpbGUgZXhlY3V0aW9uLiAqL1xuZXhwb3J0IGludGVyZmFjZSBaZXJvUGVybFJlc3VsdCB7XG4gICAgc3VjY2VzczogYm9vbGVhbjtcbiAgICBlcnJvcj86IHN0cmluZztcbiAgICBleGl0Q29kZTogbnVtYmVyO1xufVxuXG5jb25zdCB0ZXh0RGVjb2RlciA9IG5ldyBUZXh0RGVjb2RlcigpO1xuY29uc3QgdGV4dEVuY29kZXIgPSBuZXcgVGV4dEVuY29kZXIoKTtcblxubGV0IHdhc21Tb3VyY2VDYWNoZTogV2Vha1JlZjxBcnJheUJ1ZmZlcj4gfCBudWxsID0gbnVsbDtcblxuZnVuY3Rpb24gaXNCcm93c2VyKCk6IGJvb2xlYW4ge1xuICAgIC8vIFdpbmRvdyBPUiBXb3JrZXIgc2NvcGUg4oCUIGJvdGggbG9hZCB0aGUgd2FzbSB2aWEgZmV0Y2guIFdvcmtlcnNcbiAgICAvLyBoYXZlIG5laXRoZXIgd2luZG93IG5vciBkb2N1bWVudCwgYnV0IERlZGljYXRlZFdvcmtlckdsb2JhbFNjb3BlXG4gICAgLy8gYWx3YXlzIGNhcnJpZXMgaW1wb3J0U2NyaXB0cyAodHlwZW9mIFwiZnVuY3Rpb25cIiBldmVuIGluIG1vZHVsZVxuICAgIC8vIHdvcmtlcnMsIHdoZXJlIGNhbGxpbmcgaXQgdGhyb3dzKS4gV2l0aG91dCB0aGlzLCB3b3JrZXJzIGZlbGxcbiAgICAvLyBpbnRvIHRoZSBOb2RlIGJyYW5jaCBhbmQgZGllZCB0cnlpbmcgdG8gZnMtcmVhZCB0aGUgd2FzbS5cbiAgICBjb25zdCBnID0gZ2xvYmFsVGhpcyBhcyB7IGltcG9ydFNjcmlwdHM/OiB1bmtub3duIH07XG4gICAgcmV0dXJuICh0eXBlb2Ygd2luZG93ICE9PSBcInVuZGVmaW5lZFwiICYmIHR5cGVvZiBkb2N1bWVudCAhPT0gXCJ1bmRlZmluZWRcIilcbiAgICAgICAgfHwgKHR5cGVvZiBnLmltcG9ydFNjcmlwdHMgPT09IFwiZnVuY3Rpb25cIik7XG59XG5cbmFzeW5jIGZ1bmN0aW9uIGxvYWRXYXNtU291cmNlKGZldGNoRm4/OiBGZXRjaExpa2UpOiBQcm9taXNlPEFycmF5QnVmZmVyPiB7XG4gICAgaWYgKHdhc21Tb3VyY2VDYWNoZSkge1xuICAgICAgICBjb25zdCBjYWNoZWQgPSB3YXNtU291cmNlQ2FjaGUuZGVyZWYoKTtcbiAgICAgICAgaWYgKGNhY2hlZCkgcmV0dXJuIGNhY2hlZDtcbiAgICB9XG5cbiAgICBsZXQgbW9kdWxlRGF0YTogQXJyYXlCdWZmZXI7XG5cbiAgICBpZiAoaXNCcm93c2VyKCkpIHtcbiAgICAgICAgY29uc3QgZiA9IGZldGNoRm4gPz8gZmV0Y2g7XG4gICAgICAgIGNvbnN0IHJlc3BvbnNlID0gYXdhaXQgZih6ZXJvcGVybCk7XG4gICAgICAgIG1vZHVsZURhdGEgPSBhd2FpdCByZXNwb25zZS5hcnJheUJ1ZmZlcigpO1xuICAgIH0gZWxzZSB7XG4gICAgICAgIGNvbnN0IHdhc21VcmwgPSBuZXcgVVJMKHplcm9wZXJsLCBpbXBvcnQubWV0YS51cmwpO1xuICAgICAgICBjb25zdCB3YXNtUGF0aCA9IHdhc21VcmwucGF0aG5hbWU7XG5cbiAgICAgICAgLy9AdHMtZXhwZWN0LWVycm9yIERlbm9cbiAgICAgICAgaWYgKHR5cGVvZiBEZW5vICE9PSBcInVuZGVmaW5lZFwiKSB7XG4gICAgICAgICAgICAvL0B0cy1leHBlY3QtZXJyb3IgRGVub1xuICAgICAgICAgICAgbW9kdWxlRGF0YSA9IChhd2FpdCBEZW5vLnJlYWRGaWxlKHdhc21QYXRoKSkuYnVmZmVyO1xuICAgICAgICB9IGVsc2UgaWYgKHR5cGVvZiBCdW4gIT09IFwidW5kZWZpbmVkXCIpIHtcbiAgICAgICAgICAgIGNvbnN0IGZpbGUgPSBCdW4uZmlsZSh3YXNtUGF0aCk7XG4gICAgICAgICAgICBtb2R1bGVEYXRhID0gYXdhaXQgZmlsZS5hcnJheUJ1ZmZlcigpO1xuICAgICAgICB9IGVsc2Uge1xuICAgICAgICAgICAgY29uc3QgeyByZWFkRmlsZSB9ID0gYXdhaXQgaW1wb3J0KFwibm9kZTpmcy9wcm9taXNlc1wiKTtcbiAgICAgICAgICAgIG1vZHVsZURhdGEgPSAoYXdhaXQgcmVhZEZpbGUod2FzbVBhdGgpKS5idWZmZXI7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICB3YXNtU291cmNlQ2FjaGUgPSBuZXcgV2Vha1JlZihtb2R1bGVEYXRhKTtcbiAgICByZXR1cm4gbW9kdWxlRGF0YTtcbn1cblxuZnVuY3Rpb24gbWFwUGVybFR5cGUodHlwZUNvZGU6IG51bWJlcik6IFBlcmxWYWx1ZVR5cGUge1xuICAgIGNvbnN0IHR5cGVzOiBQZXJsVmFsdWVUeXBlW10gPSBbXG4gICAgICAgIFwidW5kZWZcIiwgXCJ0cnVlXCIsIFwiZmFsc2VcIiwgXCJpbnRcIiwgXCJkb3VibGVcIixcbiAgICAgICAgXCJzdHJpbmdcIiwgXCJhcnJheVwiLCBcImhhc2hcIiwgXCJjb2RlXCIsIFwicmVmXCIsXG4gICAgXTtcbiAgICByZXR1cm4gdHlwZXNbdHlwZUNvZGVdIHx8IFwidW5kZWZcIjtcbn1cblxuZnVuY3Rpb24gbWFwQ29udGV4dChjb250ZXh0OiBQZXJsQ29udGV4dCk6IG51bWJlciB7XG4gICAgcmV0dXJuIHsgdm9pZDogMCwgc2NhbGFyOiAxLCBsaXN0OiAyIH1bY29udGV4dF07XG59XG5cbi8qKlxuICogV3JhcHBlciBmb3IgUGVybCBzY2FsYXIgdmFsdWVzLlxuICpcbiAqIFJlcHJlc2VudHMgYW55IFBlcmwgc2NhbGFyIHZhbHVlIChpbnRlZ2VycywgZmxvYXRzLCBzdHJpbmdzLCByZWZlcmVuY2VzLCBldGMpLlxuICogQWxsIG9wZXJhdGlvbnMgYXJlIHN5bmNocm9ub3VzLlxuICpcbiAqIE1lbW9yeSBtdXN0IGJlIGV4cGxpY2l0bHkgZnJlZWQgYnkgY2FsbGluZyBkaXNwb3NlKCkuXG4gKlxuICogQGV4YW1wbGVcbiAqIGBgYHR5cGVzY3JpcHRcbiAqIGNvbnN0IG51bSA9IHBlcmwuY3JlYXRlSW50KDQyKTtcbiAqIGNvbnNvbGUubG9nKG51bS5nZXRUeXBlKCkpOyAvLyAnaW50J1xuICogY29uc29sZS5sb2cobnVtLnRvSW50KCkpOyAvLyA0MlxuICogY29uc29sZS5sb2cobnVtLnRvU3RyaW5nKCkpOyAvLyBcIjQyXCJcbiAqIGNvbnNvbGUubG9nKG51bS5wcm9qZWN0KCkpOyAvLyA0MlxuICogbnVtLmRpc3Bvc2UoKTtcbiAqIGBgYFxuICovXG5leHBvcnQgY2xhc3MgUGVybFZhbHVlIHtcbiAgICBwcml2YXRlIHB0cjogbnVtYmVyO1xuICAgIHByaXZhdGUgZXhwb3J0czogWmVyb1BlcmxFeHBvcnRzO1xuICAgIHByaXZhdGUgZGlzcG9zZWQgPSBmYWxzZTtcblxuICAgIC8qKiBAaW50ZXJuYWwgKi9cbiAgICBjb25zdHJ1Y3RvcihwdHI6IG51bWJlciwgZXhwb3J0czogWmVyb1BlcmxFeHBvcnRzKSB7XG4gICAgICAgIHRoaXMucHRyID0gcHRyO1xuICAgICAgICB0aGlzLmV4cG9ydHMgPSBleHBvcnRzO1xuICAgIH1cblxuICAgIC8qKiBAaW50ZXJuYWwgKi9cbiAgICBnZXRQdHIoKTogbnVtYmVyIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHJldHVybiB0aGlzLnB0cjtcbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBDb252ZXJ0IHZhbHVlIHRvIGEgMzItYml0IGludGVnZXIuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgY29udmVyc2lvbiBmYWlsc1xuICAgICAqL1xuICAgIHRvSW50KCk6IG51bWJlciB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBvdXRQdHIgPSB0aGlzLmV4cG9ydHMubWFsbG9jKDQpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgaWYgKCF0aGlzLmV4cG9ydHMuemVyb3BlcmxfdG9faW50KHRoaXMucHRyLCBvdXRQdHIpKSB7XG4gICAgICAgICAgICAgICAgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gY29udmVydCB2YWx1ZSB0byBpbnRcIik7XG4gICAgICAgICAgICB9XG4gICAgICAgICAgICByZXR1cm4gbmV3IERhdGFWaWV3KHRoaXMuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKS5nZXRJbnQzMihvdXRQdHIsIHRydWUpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUob3V0UHRyKTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKlxuICAgICAqIENvbnZlcnQgdmFsdWUgdG8gYSBkb3VibGUtcHJlY2lzaW9uIGZsb2F0LlxuICAgICAqIEB0aHJvd3Mge1plcm9QZXJsRXJyb3J9IElmIGNvbnZlcnNpb24gZmFpbHNcbiAgICAgKi9cbiAgICB0b0RvdWJsZSgpOiBudW1iZXIge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3Qgb3V0UHRyID0gdGhpcy5leHBvcnRzLm1hbGxvYyg4KTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIGlmICghdGhpcy5leHBvcnRzLnplcm9wZXJsX3RvX2RvdWJsZSh0aGlzLnB0ciwgb3V0UHRyKSkge1xuICAgICAgICAgICAgICAgIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNvbnZlcnQgdmFsdWUgdG8gZG91YmxlXCIpO1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgcmV0dXJuIG5ldyBEYXRhVmlldyh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcikuZ2V0RmxvYXQ2NChvdXRQdHIsIHRydWUpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUob3V0UHRyKTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKiBDb252ZXJ0IHZhbHVlIHRvIGEgVVRGLTggc3RyaW5nLiAqL1xuICAgIHRvU3RyaW5nKCk6IHN0cmluZyB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBsZW5QdHIgPSB0aGlzLmV4cG9ydHMubWFsbG9jKDQpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3Qgc3RyUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX3RvX3N0cmluZyh0aGlzLnB0ciwgbGVuUHRyKTtcbiAgICAgICAgICAgIGlmIChzdHJQdHIgPT09IDApIHJldHVybiBcIlwiO1xuICAgICAgICAgICAgY29uc3QgbGVuID0gbmV3IERhdGFWaWV3KHRoaXMuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKS5nZXRVaW50MzIobGVuUHRyLCB0cnVlKTtcbiAgICAgICAgICAgIHJldHVybiB0ZXh0RGVjb2Rlci5kZWNvZGUobmV3IFVpbnQ4QXJyYXkodGhpcy5leHBvcnRzLm1lbW9yeS5idWZmZXIsIHN0clB0ciwgbGVuKSk7XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShsZW5QdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIENvbnZlcnQgdmFsdWUgdG8gYSBib29sZWFuIHVzaW5nIFBlcmwncyB0cnV0aCB0ZXN0LiAqL1xuICAgIHRvQm9vbGVhbigpOiBib29sZWFuIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHJldHVybiB0aGlzLmV4cG9ydHMuemVyb3BlcmxfdG9fYm9vbCh0aGlzLnB0cikgIT09IDA7XG4gICAgfVxuXG4gICAgLyoqIENoZWNrIGlmIHZhbHVlIGlzIHVuZGVmaW5lZC4gKi9cbiAgICBpc1VuZGVmKCk6IGJvb2xlYW4ge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgcmV0dXJuIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9pc191bmRlZih0aGlzLnB0cikgIT09IDA7XG4gICAgfVxuXG4gICAgLyoqIENoZWNrIGlmIHZhbHVlIGlzIGEgcmVmZXJlbmNlLiAqL1xuICAgIGlzUmVmKCk6IGJvb2xlYW4ge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgcmV0dXJuIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9pc19yZWYodGhpcy5wdHIpICE9PSAwO1xuICAgIH1cblxuICAgIC8qKiBHZXQgdGhlIHR5cGUgb2YgdGhpcyB2YWx1ZS4gKi9cbiAgICBnZXRUeXBlKCk6IFBlcmxWYWx1ZVR5cGUge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgcmV0dXJuIG1hcFBlcmxUeXBlKHRoaXMuZXhwb3J0cy56ZXJvcGVybF9nZXRfdHlwZSh0aGlzLnB0cikpO1xuICAgIH1cblxuICAgIC8qKlxuICAgICAqIENvbnZlcnQgdGhpcyBQZXJsIHZhbHVlIHRvIGEgSmF2YVNjcmlwdCBwcmltaXRpdmUuXG4gICAgICpcbiAgICAgKiBDb252ZXJzaW9uIHJ1bGVzOlxuICAgICAqIC0gdW5kZWYg4oaSIG51bGxcbiAgICAgKiAtIGludC9kb3VibGUg4oaSIG51bWJlclxuICAgICAqIC0gc3RyaW5nIOKGkiBzdHJpbmdcbiAgICAgKiAtIHRydWUvZmFsc2Ug4oaSIGJvb2xlYW5cbiAgICAgKiAtIE90aGVyIHR5cGVzIOKGkiBzdHJpbmcgcmVwcmVzZW50YXRpb25cbiAgICAgKi9cbiAgICBwcm9qZWN0KCk6IEpTUHJpbWl0aXZlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGlmICh0aGlzLmlzVW5kZWYoKSkgcmV0dXJuIG51bGw7XG4gICAgICAgIGNvbnN0IHR5cGUgPSB0aGlzLmdldFR5cGUoKTtcbiAgICAgICAgc3dpdGNoICh0eXBlKSB7XG4gICAgICAgICAgICBjYXNlICd0cnVlJzogcmV0dXJuIHRydWU7XG4gICAgICAgICAgICBjYXNlICdmYWxzZSc6IHJldHVybiBmYWxzZTtcbiAgICAgICAgICAgIGNhc2UgXCJpbnRcIjpcbiAgICAgICAgICAgIGNhc2UgXCJkb3VibGVcIjogcmV0dXJuIHRoaXMudG9Eb3VibGUoKTtcbiAgICAgICAgICAgIGNhc2UgXCJzdHJpbmdcIjogcmV0dXJuIHRoaXMudG9TdHJpbmcoKTtcbiAgICAgICAgICAgIGRlZmF1bHQ6IHJldHVybiB0aGlzLnRvU3RyaW5nKCk7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBDcmVhdGUgYSByZWZlcmVuY2UgdG8gdGhpcyB2YWx1ZS5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiByZWZlcmVuY2UgY3JlYXRpb24gZmFpbHNcbiAgICAgKi9cbiAgICBjcmVhdGVSZWYoKTogUGVybFZhbHVlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHJlZlB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9uZXdfcmVmKHRoaXMucHRyKTtcbiAgICAgICAgaWYgKHJlZlB0ciA9PT0gMCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gY3JlYXRlIHJlZmVyZW5jZVwiKTtcbiAgICAgICAgcmV0dXJuIG5ldyBQZXJsVmFsdWUocmVmUHRyLCB0aGlzLmV4cG9ydHMpO1xuICAgIH1cblxuICAgIC8qKlxuICAgICAqIERlcmVmZXJlbmNlIHRoaXMgdmFsdWUuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgdmFsdWUgaXMgbm90IGEgcmVmZXJlbmNlXG4gICAgICovXG4gICAgZGVyZWYoKTogUGVybFZhbHVlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IGRlcmVmUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2RlcmVmKHRoaXMucHRyKTtcbiAgICAgICAgaWYgKGRlcmVmUHRyID09PSAwKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihcIkZhaWxlZCB0byBkZXJlZmVyZW5jZSB2YWx1ZVwiKTtcbiAgICAgICAgcmV0dXJuIG5ldyBQZXJsVmFsdWUoZGVyZWZQdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqIEluY3JlbWVudCB0aGUgcmVmZXJlbmNlIGNvdW50LiAqL1xuICAgIGluY3JlZigpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9pbmNyZWYodGhpcy5wdHIpO1xuICAgIH1cblxuICAgIC8qKiBEZWNyZW1lbnQgdGhlIHJlZmVyZW5jZSBjb3VudC4gKi9cbiAgICBkZWNyZWYoKTogdm9pZCB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3BlcmxfZGVjcmVmKHRoaXMucHRyKTtcbiAgICB9XG5cbiAgICAvKiogRnJlZSB0aGlzIHZhbHVlJ3MgbWVtb3J5LiBBZnRlciBjYWxsaW5nLCB0aGlzIHZhbHVlIGNhbm5vdCBiZSB1c2VkLiAqL1xuICAgIGRpc3Bvc2UoKTogdm9pZCB7XG4gICAgICAgIGlmICh0aGlzLmRpc3Bvc2VkKSByZXR1cm47XG4gICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF92YWx1ZV9mcmVlKHRoaXMucHRyKTtcbiAgICAgICAgdGhpcy5kaXNwb3NlZCA9IHRydWU7XG4gICAgfVxuXG4gICAgcHJpdmF0ZSBjaGVja0Rpc3Bvc2VkKCk6IHZvaWQge1xuICAgICAgICBpZiAodGhpcy5kaXNwb3NlZCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJQZXJsVmFsdWUgaGFzIGJlZW4gZGlzcG9zZWRcIik7XG4gICAgfVxufVxuXG4vKipcbiAqIFdyYXBwZXIgZm9yIFBlcmwgYXJyYXlzLlxuICpcbiAqIFByb3ZpZGVzIHB1c2gvcG9wIG9wZXJhdGlvbnMsIGluZGV4aW5nLCBpdGVyYXRpb24sIGFuZCBjb252ZXJzaW9uXG4gKiB0by9mcm9tIEphdmFTY3JpcHQgYXJyYXlzLiBBbGwgb3BlcmF0aW9ucyBhcmUgc3luY2hyb25vdXMuXG4gKlxuICogTWVtb3J5IG11c3QgYmUgZXhwbGljaXRseSBmcmVlZCBieSBjYWxsaW5nIGRpc3Bvc2UoKS5cbiAqXG4gKiBAZXhhbXBsZVxuICogYGBgdHlwZXNjcmlwdFxuICogY29uc3QgYXJyID0gcGVybC5jcmVhdGVBcnJheShbMSwgMiwgMywgXCJoZWxsb1wiLCB0cnVlXSk7XG4gKiBjb25zb2xlLmxvZyhhcnIuZ2V0TGVuZ3RoKCkpOyAvLyA1XG4gKiBjb25zb2xlLmxvZyhhcnIuZ2V0KDApPy50b0ludCgpKTsgLy8gMVxuICogY29uc3QganNBcnJheSA9IGFyci5wcm9qZWN0KCk7IC8vIFsxLCAyLCAzLCBcImhlbGxvXCIsIHRydWVdXG4gKiBhcnIuZGlzcG9zZSgpO1xuICogYGBgXG4gKi9cbmV4cG9ydCBjbGFzcyBQZXJsQXJyYXkge1xuICAgIHByaXZhdGUgcHRyOiBudW1iZXI7XG4gICAgcHJpdmF0ZSBleHBvcnRzOiBaZXJvUGVybEV4cG9ydHM7XG4gICAgcHJpdmF0ZSBwZXJsOiBaZXJvUGVybDtcbiAgICBwcml2YXRlIGRpc3Bvc2VkID0gZmFsc2U7XG5cbiAgICAvKiogQGludGVybmFsICovXG4gICAgY29uc3RydWN0b3IocHRyOiBudW1iZXIsIGV4cG9ydHM6IFplcm9QZXJsRXhwb3J0cywgcGVybDogWmVyb1BlcmwpIHtcbiAgICAgICAgdGhpcy5wdHIgPSBwdHI7XG4gICAgICAgIHRoaXMuZXhwb3J0cyA9IGV4cG9ydHM7XG4gICAgICAgIHRoaXMucGVybCA9IHBlcmw7XG4gICAgfVxuXG4gICAgLyoqIEBpbnRlcm5hbCAqL1xuICAgIGdldFB0cigpOiBudW1iZXIge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgcmV0dXJuIHRoaXMucHRyO1xuICAgIH1cblxuICAgIC8qKiBQdXNoIGEgdmFsdWUgb250byB0aGUgZW5kIG9mIHRoZSBhcnJheS4gKi9cbiAgICBwdXNoKHZhbHVlOiBQZXJsQ29udmVydGlibGUpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHBlcmxWYWx1ZSA9IHRoaXMucGVybC50b1BlcmxWYWx1ZSh2YWx1ZSk7XG4gICAgICAgIHRyeSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3BlcmxfYXJyYXlfcHVzaCh0aGlzLnB0ciwgcGVybFZhbHVlLmdldFB0cigpKTtcbiAgICAgICAgfSBmaW5hbGx5IHtcbiAgICAgICAgICAgIGlmICghKHZhbHVlIGluc3RhbmNlb2YgUGVybFZhbHVlKSkgcGVybFZhbHVlLmRpc3Bvc2UoKTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKiBQb3AgYSB2YWx1ZSBmcm9tIHRoZSBlbmQgb2YgdGhlIGFycmF5LiBSZXR1cm5zIG51bGwgaWYgZW1wdHkuICovXG4gICAgcG9wKCk6IFBlcmxWYWx1ZSB8IG51bGwge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgdmFsUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2FycmF5X3BvcCh0aGlzLnB0cik7XG4gICAgICAgIHJldHVybiB2YWxQdHIgPT09IDAgPyBudWxsIDogbmV3IFBlcmxWYWx1ZSh2YWxQdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqIEdldCBhIHZhbHVlIGF0IHRoZSBzcGVjaWZpZWQgaW5kZXguIFJldHVybnMgbnVsbCBpZiBvdXQgb2YgYm91bmRzLiAqL1xuICAgIGdldChpbmRleDogbnVtYmVyKTogUGVybFZhbHVlIHwgbnVsbCB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCB2YWxQdHIgPSB0aGlzLmV4cG9ydHMuemVyb3BlcmxfYXJyYXlfZ2V0KHRoaXMucHRyLCBpbmRleCk7XG4gICAgICAgIHJldHVybiB2YWxQdHIgPT09IDAgPyBudWxsIDogbmV3IFBlcmxWYWx1ZSh2YWxQdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogU2V0IGEgdmFsdWUgYXQgdGhlIHNwZWNpZmllZCBpbmRleC5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBpbmRleCBpcyBpbnZhbGlkXG4gICAgICovXG4gICAgc2V0KGluZGV4OiBudW1iZXIsIHZhbHVlOiBQZXJsQ29udmVydGlibGUpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHBlcmxWYWx1ZSA9IHRoaXMucGVybC50b1BlcmxWYWx1ZSh2YWx1ZSk7XG4gICAgICAgIHRyeSB7XG4gICAgICAgICAgICBpZiAoIXRoaXMuZXhwb3J0cy56ZXJvcGVybF9hcnJheV9zZXQodGhpcy5wdHIsIGluZGV4LCBwZXJsVmFsdWUuZ2V0UHRyKCkpKSB7XG4gICAgICAgICAgICAgICAgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoYEZhaWxlZCB0byBzZXQgYXJyYXkgZWxlbWVudCBhdCBpbmRleCAke2luZGV4fWApO1xuICAgICAgICAgICAgfVxuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgaWYgKCEodmFsdWUgaW5zdGFuY2VvZiBQZXJsVmFsdWUpKSBwZXJsVmFsdWUuZGlzcG9zZSgpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIEdldCB0aGUgbGVuZ3RoIG9mIHRoZSBhcnJheS4gKi9cbiAgICBnZXRMZW5ndGgoKTogbnVtYmVyIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHJldHVybiB0aGlzLmV4cG9ydHMuemVyb3BlcmxfYXJyYXlfbGVuZ3RoKHRoaXMucHRyKTtcbiAgICB9XG5cbiAgICAvKiogQ2xlYXIgYWxsIGVsZW1lbnRzIGZyb20gdGhlIGFycmF5LiAqL1xuICAgIGNsZWFyKCk6IHZvaWQge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgdGhpcy5leHBvcnRzLnplcm9wZXJsX2FycmF5X2NsZWFyKHRoaXMucHRyKTtcbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBDb252ZXJ0IHRoaXMgYXJyYXkgdG8gYSBQZXJsVmFsdWUgKGFycmF5IHJlZmVyZW5jZSkuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgY29udmVyc2lvbiBmYWlsc1xuICAgICAqL1xuICAgIHRvVmFsdWUoKTogUGVybFZhbHVlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHZhbFB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9hcnJheV90b192YWx1ZSh0aGlzLnB0cik7XG4gICAgICAgIGlmICh2YWxQdHIgPT09IDApIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNvbnZlcnQgYXJyYXkgdG8gdmFsdWVcIik7XG4gICAgICAgIHJldHVybiBuZXcgUGVybFZhbHVlKHZhbFB0ciwgdGhpcy5leHBvcnRzKTtcbiAgICB9XG5cbiAgICAvKiogQ29udmVydCB0aGlzIFBlcmwgYXJyYXkgdG8gYSBKYXZhU2NyaXB0IGFycmF5IG9mIHByaW1pdGl2ZXMuICovXG4gICAgcHJvamVjdCgpOiBKU1ByaW1pdGl2ZVtdIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IGxlbiA9IHRoaXMuZ2V0TGVuZ3RoKCk7XG4gICAgICAgIGNvbnN0IHJlc3VsdDogSlNQcmltaXRpdmVbXSA9IFtdO1xuICAgICAgICBmb3IgKGxldCBpID0gMDsgaSA8IGxlbjsgaSsrKSB7XG4gICAgICAgICAgICBjb25zdCB2YWwgPSB0aGlzLmdldChpKTtcbiAgICAgICAgICAgIGlmICh2YWwpIHtcbiAgICAgICAgICAgICAgICByZXN1bHQucHVzaCh2YWwucHJvamVjdCgpKTtcbiAgICAgICAgICAgICAgICB2YWwuZGlzcG9zZSgpO1xuICAgICAgICAgICAgfSBlbHNlIHtcbiAgICAgICAgICAgICAgICByZXN1bHQucHVzaChudWxsKTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgfVxuICAgICAgICByZXR1cm4gcmVzdWx0O1xuICAgIH1cblxuICAgIC8qKiBAaW50ZXJuYWwgKi9cbiAgICBzdGF0aWMgZnJvbVZhbHVlKHZhbHVlOiBQZXJsVmFsdWUsIHBlcmw6IFplcm9QZXJsKTogUGVybEFycmF5IHwgbnVsbCB7XG4gICAgICAgIGNvbnN0IGV4cG9ydHMgPSAodmFsdWUgYXMgdW5rbm93biBhcyB7IGV4cG9ydHM6IFplcm9QZXJsRXhwb3J0cyB9KS5leHBvcnRzO1xuICAgICAgICBjb25zdCBhcnJQdHIgPSBleHBvcnRzLnplcm9wZXJsX3ZhbHVlX3RvX2FycmF5KHZhbHVlLmdldFB0cigpKTtcbiAgICAgICAgcmV0dXJuIGFyclB0ciA9PT0gMCA/IG51bGwgOiBuZXcgUGVybEFycmF5KGFyclB0ciwgZXhwb3J0cywgcGVybCk7XG4gICAgfVxuXG4gICAgLyoqIEl0ZXJhdGUgb3ZlciBhbGwgdmFsdWVzIGluIHRoZSBhcnJheS4gUmVtZW1iZXIgdG8gZGlzcG9zZSB5aWVsZGVkIHZhbHVlcy4gKi9cbiAgICAqW1N5bWJvbC5pdGVyYXRvcl0oKTogR2VuZXJhdG9yPFBlcmxWYWx1ZSwgdm9pZCwgdW5kZWZpbmVkPiB7XG4gICAgICAgIGNvbnN0IGxlbiA9IHRoaXMuZ2V0TGVuZ3RoKCk7XG4gICAgICAgIGZvciAobGV0IGkgPSAwOyBpIDwgbGVuOyBpKyspIHtcbiAgICAgICAgICAgIGNvbnN0IHZhbCA9IHRoaXMuZ2V0KGkpO1xuICAgICAgICAgICAgaWYgKHZhbCkgeWllbGQgdmFsO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIEZyZWUgdGhpcyBhcnJheSdzIG1lbW9yeS4gQWZ0ZXIgY2FsbGluZywgdGhpcyBhcnJheSBjYW5ub3QgYmUgdXNlZC4gKi9cbiAgICBkaXNwb3NlKCk6IHZvaWQge1xuICAgICAgICBpZiAodGhpcy5kaXNwb3NlZCkgcmV0dXJuO1xuICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3BlcmxfYXJyYXlfZnJlZSh0aGlzLnB0cik7XG4gICAgICAgIHRoaXMuZGlzcG9zZWQgPSB0cnVlO1xuICAgIH1cblxuICAgIHByaXZhdGUgY2hlY2tEaXNwb3NlZCgpOiB2b2lkIHtcbiAgICAgICAgaWYgKHRoaXMuZGlzcG9zZWQpIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiUGVybEFycmF5IGhhcyBiZWVuIGRpc3Bvc2VkXCIpO1xuICAgIH1cbn1cblxuLyoqXG4gKiBXcmFwcGVyIGZvciBQZXJsIGhhc2hlcy5cbiAqXG4gKiBQcm92aWRlcyBhIE1hcC1saWtlIGludGVyZmFjZSB3aXRoIGl0ZXJhdGlvbiBtZXRob2RzIGFuZCBjb252ZXJzaW9uXG4gKiB0by9mcm9tIEphdmFTY3JpcHQgb2JqZWN0cy4gQWxsIG9wZXJhdGlvbnMgYXJlIHN5bmNocm9ub3VzLlxuICpcbiAqIE1lbW9yeSBtdXN0IGJlIGV4cGxpY2l0bHkgZnJlZWQgYnkgY2FsbGluZyBkaXNwb3NlKCkuXG4gKlxuICogQGV4YW1wbGVcbiAqIGBgYHR5cGVzY3JpcHRcbiAqIGNvbnN0IGhhc2ggPSBwZXJsLmNyZWF0ZUhhc2goeyBuYW1lOiAnQWxpY2UnLCBhZ2U6IDMwLCBhY3RpdmU6IHRydWUgfSk7XG4gKiBjb25zb2xlLmxvZyhoYXNoLmdldCgnbmFtZScpPy50b1N0cmluZygpKTsgLy8gXCJBbGljZVwiXG4gKiBjb25zdCBvYmogPSBoYXNoLnByb2plY3QoKTsgLy8geyBuYW1lOiAnQWxpY2UnLCBhZ2U6IDMwLCBhY3RpdmU6IHRydWUgfVxuICogaGFzaC5kaXNwb3NlKCk7XG4gKiBgYGBcbiAqL1xuZXhwb3J0IGNsYXNzIFBlcmxIYXNoIHtcbiAgICBwcml2YXRlIHB0cjogbnVtYmVyO1xuICAgIHByaXZhdGUgZXhwb3J0czogWmVyb1BlcmxFeHBvcnRzO1xuICAgIHByaXZhdGUgcGVybDogWmVyb1Blcmw7XG4gICAgcHJpdmF0ZSBkaXNwb3NlZCA9IGZhbHNlO1xuXG4gICAgLyoqIEBpbnRlcm5hbCAqL1xuICAgIGNvbnN0cnVjdG9yKHB0cjogbnVtYmVyLCBleHBvcnRzOiBaZXJvUGVybEV4cG9ydHMsIHBlcmw6IFplcm9QZXJsKSB7XG4gICAgICAgIHRoaXMucHRyID0gcHRyO1xuICAgICAgICB0aGlzLmV4cG9ydHMgPSBleHBvcnRzO1xuICAgICAgICB0aGlzLnBlcmwgPSBwZXJsO1xuICAgIH1cblxuICAgIC8qKiBAaW50ZXJuYWwgKi9cbiAgICBnZXRQdHIoKTogbnVtYmVyIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHJldHVybiB0aGlzLnB0cjtcbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBTZXQgYSBrZXktdmFsdWUgcGFpciBpbiB0aGUgaGFzaC5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBzZXR0aW5nIHRoZSBrZXkgZmFpbHNcbiAgICAgKi9cbiAgICBzZXQoa2V5OiBzdHJpbmcsIHZhbHVlOiBQZXJsQ29udmVydGlibGUpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHBlcmxWYWx1ZSA9IHRoaXMucGVybC50b1BlcmxWYWx1ZSh2YWx1ZSk7XG4gICAgICAgIGNvbnN0IGtleVB0ciA9IHRoaXMud3JpdGVDU3RyaW5nKGtleSk7XG4gICAgICAgIHRyeSB7XG4gICAgICAgICAgICBpZiAoIXRoaXMuZXhwb3J0cy56ZXJvcGVybF9oYXNoX3NldCh0aGlzLnB0ciwga2V5UHRyLCBwZXJsVmFsdWUuZ2V0UHRyKCkpKSB7XG4gICAgICAgICAgICAgICAgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoYEZhaWxlZCB0byBzZXQgaGFzaCBrZXkgJyR7a2V5fSdgKTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgfSBmaW5hbGx5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKGtleVB0cik7XG4gICAgICAgICAgICBpZiAoISh2YWx1ZSBpbnN0YW5jZW9mIFBlcmxWYWx1ZSkpIHBlcmxWYWx1ZS5kaXNwb3NlKCk7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKiogR2V0IGEgdmFsdWUgYnkga2V5LiBSZXR1cm5zIG51bGwgaWYga2V5IGRvZXNuJ3QgZXhpc3QuICovXG4gICAgZ2V0KGtleTogc3RyaW5nKTogUGVybFZhbHVlIHwgbnVsbCB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBrZXlQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhrZXkpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgdmFsUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2hhc2hfZ2V0KHRoaXMucHRyLCBrZXlQdHIpO1xuICAgICAgICAgICAgcmV0dXJuIHZhbFB0ciA9PT0gMCA/IG51bGwgOiBuZXcgUGVybFZhbHVlKHZhbFB0ciwgdGhpcy5leHBvcnRzKTtcbiAgICAgICAgfSBmaW5hbGx5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKGtleVB0cik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKiogQ2hlY2sgaWYgYSBrZXkgZXhpc3RzIGluIHRoZSBoYXNoLiAqL1xuICAgIGhhcyhrZXk6IHN0cmluZyk6IGJvb2xlYW4ge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3Qga2V5UHRyID0gdGhpcy53cml0ZUNTdHJpbmcoa2V5KTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIHJldHVybiB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaGFzaF9leGlzdHModGhpcy5wdHIsIGtleVB0cikgIT09IDA7XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShrZXlQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIERlbGV0ZSBhIGtleSBmcm9tIHRoZSBoYXNoLiBSZXR1cm5zIHRydWUgaWYga2V5IHdhcyBkZWxldGVkLiAqL1xuICAgIGRlbGV0ZShrZXk6IHN0cmluZyk6IGJvb2xlYW4ge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3Qga2V5UHRyID0gdGhpcy53cml0ZUNTdHJpbmcoa2V5KTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIHJldHVybiB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaGFzaF9kZWxldGUodGhpcy5wdHIsIGtleVB0cikgIT09IDA7XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShrZXlQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIENsZWFyIGFsbCBlbnRyaWVzIGZyb20gdGhlIGhhc2guICovXG4gICAgY2xlYXIoKTogdm9pZCB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaGFzaF9jbGVhcih0aGlzLnB0cik7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ29udmVydCB0aGlzIGhhc2ggdG8gYSBQZXJsVmFsdWUgKGhhc2ggcmVmZXJlbmNlKS5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBjb252ZXJzaW9uIGZhaWxzXG4gICAgICovXG4gICAgdG9WYWx1ZSgpOiBQZXJsVmFsdWUge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgdmFsUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2hhc2hfdG9fdmFsdWUodGhpcy5wdHIpO1xuICAgICAgICBpZiAodmFsUHRyID09PSAwKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihcIkZhaWxlZCB0byBjb252ZXJ0IGhhc2ggdG8gdmFsdWVcIik7XG4gICAgICAgIHJldHVybiBuZXcgUGVybFZhbHVlKHZhbFB0ciwgdGhpcy5leHBvcnRzKTtcbiAgICB9XG5cbiAgICAvKiogQ29udmVydCB0aGlzIFBlcmwgaGFzaCB0byBhIEphdmFTY3JpcHQgb2JqZWN0LiAqL1xuICAgIHByb2plY3QoKTogUmVjb3JkPHN0cmluZywgSlNQcmltaXRpdmU+IHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHJlc3VsdDogUmVjb3JkPHN0cmluZywgSlNQcmltaXRpdmU+ID0ge307XG4gICAgICAgIGZvciAoY29uc3QgW2tleSwgdmFsXSBvZiB0aGlzLmVudHJpZXMoKSkge1xuICAgICAgICAgICAgcmVzdWx0W2tleV0gPSB2YWwucHJvamVjdCgpO1xuICAgICAgICAgICAgdmFsLmRpc3Bvc2UoKTtcbiAgICAgICAgfVxuICAgICAgICByZXR1cm4gcmVzdWx0O1xuICAgIH1cblxuICAgIC8qKiBAaW50ZXJuYWwgKi9cbiAgICBzdGF0aWMgZnJvbVZhbHVlKHZhbHVlOiBQZXJsVmFsdWUsIHBlcmw6IFplcm9QZXJsKTogUGVybEhhc2ggfCBudWxsIHtcbiAgICAgICAgY29uc3QgZXhwb3J0cyA9ICh2YWx1ZSBhcyB1bmtub3duIGFzIHsgZXhwb3J0czogWmVyb1BlcmxFeHBvcnRzIH0pLmV4cG9ydHM7XG4gICAgICAgIGNvbnN0IGhhc2hQdHIgPSBleHBvcnRzLnplcm9wZXJsX3ZhbHVlX3RvX2hhc2godmFsdWUuZ2V0UHRyKCkpO1xuICAgICAgICByZXR1cm4gaGFzaFB0ciA9PT0gMCA/IG51bGwgOiBuZXcgUGVybEhhc2goaGFzaFB0ciwgZXhwb3J0cywgcGVybCk7XG4gICAgfVxuXG4gICAgLyoqIEl0ZXJhdGUgb3ZlciBhbGwga2V5LXZhbHVlIHBhaXJzLiBSZW1lbWJlciB0byBkaXNwb3NlIHlpZWxkZWQgdmFsdWVzLiAqL1xuICAgICplbnRyaWVzKCk6IEdlbmVyYXRvcjxbc3RyaW5nLCBQZXJsVmFsdWVdLCB2b2lkLCB1bmRlZmluZWQ+IHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IGl0ZXJQdHIgPSB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaGFzaF9pdGVyX25ldyh0aGlzLnB0cik7XG4gICAgICAgIGlmIChpdGVyUHRyID09PSAwKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihcIkZhaWxlZCB0byBjcmVhdGUgaGFzaCBpdGVyYXRvclwiKTtcblxuICAgICAgICBjb25zdCBrZXlPdXRQdHIgPSB0aGlzLmV4cG9ydHMubWFsbG9jKDQpO1xuICAgICAgICBjb25zdCB2YWxPdXRQdHIgPSB0aGlzLmV4cG9ydHMubWFsbG9jKDQpO1xuXG4gICAgICAgIHRyeSB7XG4gICAgICAgICAgICB3aGlsZSAodGhpcy5leHBvcnRzLnplcm9wZXJsX2hhc2hfaXRlcl9uZXh0KGl0ZXJQdHIsIGtleU91dFB0ciwgdmFsT3V0UHRyKSkge1xuICAgICAgICAgICAgICAgIGNvbnN0IHZpZXcgPSBuZXcgRGF0YVZpZXcodGhpcy5leHBvcnRzLm1lbW9yeS5idWZmZXIpO1xuICAgICAgICAgICAgICAgIGNvbnN0IGtleVB0ciA9IHZpZXcuZ2V0VWludDMyKGtleU91dFB0ciwgdHJ1ZSk7XG4gICAgICAgICAgICAgICAgY29uc3QgdmFsUHRyID0gdmlldy5nZXRVaW50MzIodmFsT3V0UHRyLCB0cnVlKTtcbiAgICAgICAgICAgICAgICB5aWVsZCBbdGhpcy5yZWFkQ1N0cmluZyhrZXlQdHIpLCBuZXcgUGVybFZhbHVlKHZhbFB0ciwgdGhpcy5leHBvcnRzKV07XG4gICAgICAgICAgICB9XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShrZXlPdXRQdHIpO1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUodmFsT3V0UHRyKTtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9oYXNoX2l0ZXJfZnJlZShpdGVyUHRyKTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKiBJdGVyYXRlIG92ZXIgYWxsIGtleXMuICovXG4gICAgKmtleXMoKTogR2VuZXJhdG9yPHN0cmluZywgdm9pZCwgdW5kZWZpbmVkPiB7XG4gICAgICAgIGZvciAoY29uc3QgW2tleSwgdmFsXSBvZiB0aGlzLmVudHJpZXMoKSkge1xuICAgICAgICAgICAgdmFsLmRpc3Bvc2UoKTtcbiAgICAgICAgICAgIHlpZWxkIGtleTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKiBJdGVyYXRlIG92ZXIgYWxsIHZhbHVlcy4gUmVtZW1iZXIgdG8gZGlzcG9zZSB5aWVsZGVkIHZhbHVlcy4gKi9cbiAgICAqdmFsdWVzKCk6IEdlbmVyYXRvcjxQZXJsVmFsdWUsIHZvaWQsIHVuZGVmaW5lZD4ge1xuICAgICAgICBmb3IgKGNvbnN0IFssIHZhbF0gb2YgdGhpcy5lbnRyaWVzKCkpIHlpZWxkIHZhbDtcbiAgICB9XG5cbiAgICAvKiogRnJlZSB0aGlzIGhhc2gncyBtZW1vcnkuIEFmdGVyIGNhbGxpbmcsIHRoaXMgaGFzaCBjYW5ub3QgYmUgdXNlZC4gKi9cbiAgICBkaXNwb3NlKCk6IHZvaWQge1xuICAgICAgICBpZiAodGhpcy5kaXNwb3NlZCkgcmV0dXJuO1xuICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaGFzaF9mcmVlKHRoaXMucHRyKTtcbiAgICAgICAgdGhpcy5kaXNwb3NlZCA9IHRydWU7XG4gICAgfVxuXG4gICAgcHJpdmF0ZSB3cml0ZUNTdHJpbmcoc3RyOiBzdHJpbmcpOiBudW1iZXIge1xuICAgICAgICBjb25zdCBieXRlcyA9IHRleHRFbmNvZGVyLmVuY29kZShgJHtzdHJ9XFwwYCk7XG4gICAgICAgIGNvbnN0IHB0ciA9IHRoaXMuZXhwb3J0cy5tYWxsb2MoYnl0ZXMubGVuZ3RoKTtcbiAgICAgICAgbmV3IFVpbnQ4QXJyYXkodGhpcy5leHBvcnRzLm1lbW9yeS5idWZmZXIpLnNldChieXRlcywgcHRyKTtcbiAgICAgICAgcmV0dXJuIHB0cjtcbiAgICB9XG5cbiAgICBwcml2YXRlIHJlYWRDU3RyaW5nKHB0cjogbnVtYmVyKTogc3RyaW5nIHtcbiAgICAgICAgaWYgKHB0ciA9PT0gMCkgcmV0dXJuIFwiXCI7XG4gICAgICAgIGNvbnN0IHZpZXcgPSBuZXcgVWludDhBcnJheSh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcik7XG4gICAgICAgIGxldCBsZW4gPSAwO1xuICAgICAgICB3aGlsZSAodmlld1twdHIgKyBsZW5dICE9PSAwKSBsZW4rKztcbiAgICAgICAgcmV0dXJuIHRleHREZWNvZGVyLmRlY29kZSh2aWV3LnN1YmFycmF5KHB0ciwgcHRyICsgbGVuKSk7XG4gICAgfVxuXG4gICAgcHJpdmF0ZSBjaGVja0Rpc3Bvc2VkKCk6IHZvaWQge1xuICAgICAgICBpZiAodGhpcy5kaXNwb3NlZCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJQZXJsSGFzaCBoYXMgYmVlbiBkaXNwb3NlZFwiKTtcbiAgICB9XG59XG5cbi8qKlxuICogQGV4YW1wbGVcbiAqIGBgYHR5cGVzY3JpcHRcbiAqIGNvbnN0IHBlcmwgPSBhd2FpdCBaZXJvUGVybC5jcmVhdGUoKTtcbiAqXG4gKiBjb25zdCB1c2VyID0gcGVybC5jcmVhdGVIYXNoKHsgbmFtZTogJ0FsaWNlJywgYWdlOiAzMCB9KTtcbiAqIHBlcmwuc2V0VmFyaWFibGUoJ3VzZXInLCB1c2VyLnRvVmFsdWUoKSk7XG4gKlxuICogYXdhaXQgcGVybC5ldmFsKCdwcmludCBcIlVzZXI6ICR1c2VyLT57bmFtZX1cXG5cIicpO1xuICpcbiAqIHVzZXIuZGlzcG9zZSgpO1xuICogcGVybC5kaXNwb3NlKCk7XG4gKiBgYGBcbiAqL1xuZXhwb3J0IGNsYXNzIFplcm9QZXJsIHtcbiAgICBwcml2YXRlIHdhc2k6IFdBU0k7XG4gICAgcHJpdmF0ZSBpc0Rpc3Bvc2VkID0gZmFsc2U7XG4gICAgcHJpdmF0ZSBob3N0RnVuY3Rpb25zOiBNYXA8bnVtYmVyLCBIb3N0RnVuY3Rpb24+ID0gbmV3IE1hcCgpO1xuICAgIHByaXZhdGUgbmV4dEZ1bmNJZCA9IDE7XG5cblxuICAgIHByaXZhdGUgY29uc3RydWN0b3Iod2FzaTogV0FTSSkge1xuICAgICAgICB0aGlzLndhc2kgPSB3YXNpO1xuICAgIH1cblxuICAgIHByaXZhdGUgZ2V0IGV4cG9ydHMoKTogWmVyb1BlcmxFeHBvcnRzIHtcbiAgICAgICAgcmV0dXJuIHRoaXMud2FzaS5leHBvcnRzIGFzIHVua25vd24gYXMgWmVyb1BlcmxFeHBvcnRzO1xuICAgIH1cblxuICAgIC8qKlxuICAgICAqIENyZWF0ZSBhIG5ldyBaZXJvUGVybCBpbnN0YW5jZS5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBpbml0aWFsaXphdGlvbiBmYWlsc1xuICAgICAqL1xuICAgIHN0YXRpYyBhc3luYyBjcmVhdGUob3B0aW9uczogWmVyb1BlcmxPcHRpb25zID0ge30pOiBQcm9taXNlPFplcm9QZXJsPiB7XG4gICAgICAgIGNvbnN0IHNvdXJjZSA9IGF3YWl0IGxvYWRXYXNtU291cmNlKG9wdGlvbnMuZmV0Y2gpO1xuICAgICAgICBjb25zdCBmaWxlU3lzdGVtID0gb3B0aW9ucy5maWxlU3lzdGVtIHx8IG5ldyBNZW1vcnlGaWxlU3lzdGVtKHsgXCIvXCI6IFwiXCIgfSk7XG5cbiAgICAgICAgY29uc3Qgd2FzaU9wdGlvbnM6IFdBU0lPcHRpb25zID0ge1xuICAgICAgICAgICAgZW52OiBvcHRpb25zLmVudiB8fCB7fSxcbiAgICAgICAgICAgIGFyZ3M6IFtcInplcm9wZXJsXCJdLFxuICAgICAgICAgICAgZmVhdHVyZXM6IFtcbiAgICAgICAgICAgICAgICB1c2VFbnZpcm9uLCB1c2VBcmdzLCB1c2VSYW5kb20sIHVzZUNsb2NrLCB1c2VQcm9jLFxuICAgICAgICAgICAgICAgIHVzZU1lbW9yeUZTKHtcbiAgICAgICAgICAgICAgICAgICAgd2l0aEZpbGVTeXN0ZW06IGZpbGVTeXN0ZW0sXG4gICAgICAgICAgICAgICAgICAgIHdpdGhTdGRJbzoge1xuICAgICAgICAgICAgICAgICAgICAgICAgc3Rkb3V0OiAoZGF0YSkgPT4gb3B0aW9ucy5zdGRvdXQ/LihkYXRhKSxcbiAgICAgICAgICAgICAgICAgICAgICAgIHN0ZGVycjogKGRhdGEpID0+IG9wdGlvbnMuc3RkZXJyPy4oZGF0YSksXG4gICAgICAgICAgICAgICAgICAgICAgICBvdXRwdXRCdWZmZXJzOiBvcHRpb25zLm91dHB1dEJ1ZmZlcnMsXG4gICAgICAgICAgICAgICAgICAgIH0sXG4gICAgICAgICAgICAgICAgfSksXG4gICAgICAgICAgICBdLFxuICAgICAgICB9O1xuXG4gICAgICAgIGNvbnN0IHdhc2kgPSBuZXcgV0FTSSh3YXNpT3B0aW9ucyk7XG4gICAgICAgIGNvbnN0IHBlcmwgPSBuZXcgWmVyb1Blcmwod2FzaSk7XG5cbiAgICAgICAgY29uc3QgaG9zdENhbGxGdW5jdGlvbiA9IGFzeW5jIChcbiAgICAgICAgICAgIGZ1bmNJZDogbnVtYmVyLCBhcmdjOiBudW1iZXIsIGFyZ3ZQdHI6IG51bWJlcixcbiAgICAgICAgKTogUHJvbWlzZTxudW1iZXI+ID0+IHBlcmwuaGFuZGxlSG9zdENhbGwoZnVuY0lkLCBhcmdjLCBhcmd2UHRyKTtcblxuICAgICAgICBjb25zdCB7IGluc3RhbmNlIH0gPSBhd2FpdCBpbnN0YW50aWF0ZShcbiAgICAgICAgICAgIHNvdXJjZSxcbiAgICAgICAgICAgIHtcbiAgICAgICAgICAgICAgICB3YXNpX3NuYXBzaG90X3ByZXZpZXcxOiB3YXNpLndhc2lJbXBvcnQsXG4gICAgICAgICAgICAgICAgZW52OiB7IGNhbGxfaG9zdF9mdW5jdGlvbjogaG9zdENhbGxGdW5jdGlvbiB9LFxuICAgICAgICAgICAgfSxcbiAgICAgICAgICAgIHsgdW53cmFwcGVkRXhwb3J0czogU1lOQ19FWFBPUlRTIH0sXG4gICAgICAgICk7XG5cbiAgICAgICAgYXdhaXQgd2FzaS5pbml0aWFsaXplKGluc3RhbmNlKTtcbiAgICAgICAgY29uc3QgcmVzdWx0ID0gYXdhaXQgcGVybC5leHBvcnRzLnplcm9wZXJsX2luaXQoKTtcbiAgICAgICAgaWYgKHJlc3VsdCAhPT0gMCkge1xuICAgICAgICAgICAgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gaW5pdGlhbGl6ZSBQZXJsIGludGVycHJldGVyXCIsIHJlc3VsdCwgcGVybC5nZXRMYXN0RXJyb3IoKSk7XG4gICAgICAgIH1cblxuICAgICAgICByZXR1cm4gcGVybDtcbiAgICB9XG5cbiAgICBwcml2YXRlIGFzeW5jIGhhbmRsZUhvc3RDYWxsKGZ1bmNJZDogbnVtYmVyLCBhcmdjOiBudW1iZXIsIGFyZ3ZQdHI6IG51bWJlcik6IFByb21pc2U8bnVtYmVyPiB7XG4gICAgICAgIGNvbnN0IGZ1bmMgPSB0aGlzLmhvc3RGdW5jdGlvbnMuZ2V0KGZ1bmNJZCk7XG4gICAgICAgIGlmICghZnVuYykge1xuICAgICAgICAgICAgdGhpcy5zZXRIb3N0RXJyb3IoYEhvc3QgZnVuY3Rpb24gJHtmdW5jSWR9IG5vdCBmb3VuZGApO1xuICAgICAgICAgICAgcmV0dXJuIDA7XG4gICAgICAgIH1cblxuICAgICAgICB0cnkge1xuXG4gICAgICAgICAgICBjb25zdCBhcmdzOiBQZXJsVmFsdWVbXSA9IFtdO1xuICAgICAgICAgICAgaWYgKGFyZ2MgPiAwKSB7XG4gICAgICAgICAgICAgICAgY29uc3QgdmlldyA9IG5ldyBEYXRhVmlldyh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcik7XG4gICAgICAgICAgICAgICAgZm9yIChsZXQgaSA9IDA7IGkgPCBhcmdjOyBpKyspIHtcbiAgICAgICAgICAgICAgICAgICAgY29uc3QgdmFsUHRyID0gdmlldy5nZXRVaW50MzIoYXJndlB0ciArIGkgKiA0LCB0cnVlKTtcbiAgICAgICAgICAgICAgICAgICAgaWYgKHZhbFB0ciAhPT0gMCkge1xuICAgICAgICAgICAgICAgICAgICAgICAgYXJncy5wdXNoKG5ldyBQZXJsVmFsdWUodmFsUHRyLCB0aGlzLmV4cG9ydHMpKTtcbiAgICAgICAgICAgICAgICAgICAgfVxuICAgICAgICAgICAgICAgIH1cbiAgICAgICAgICAgIH1cbiAgICAgICAgICAgIGNvbnN0IHJlc3VsdCA9IGF3YWl0IGZ1bmMoLi4uYXJncyk7XG4gICAgICAgICAgICBpZiAocmVzdWx0IGluc3RhbmNlb2YgUGVybFZhbHVlKSB7XG4gICAgICAgICAgICAgICAgcmV0dXJuIHJlc3VsdC5nZXRQdHIoKTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgICAgIGNvbnN0IHVuZGVmUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX25ld191bmRlZigpO1xuICAgICAgICAgICAgaWYgKHVuZGVmUHRyID09PSAwKSB7XG4gICAgICAgICAgICAgICAgdGhpcy5zZXRIb3N0RXJyb3IoXCJGYWlsZWQgdG8gYWxsb2NhdGUgcmV0dXJuIHZhbHVlXCIpO1xuICAgICAgICAgICAgICAgIHJldHVybiAwO1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgcmV0dXJuIHVuZGVmUHRyO1xuXG4gICAgICAgIH0gY2F0Y2ggKGVycm9yKSB7XG4gICAgICAgICAgICB0aGlzLnNldEhvc3RFcnJvcihlcnJvciBpbnN0YW5jZW9mIEVycm9yID8gZXJyb3IubWVzc2FnZSA6IFN0cmluZyhlcnJvcikpO1xuICAgICAgICAgICAgcmV0dXJuIDA7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICBwcml2YXRlIHNldEhvc3RFcnJvcihtZXNzYWdlOiBzdHJpbmcpOiB2b2lkIHtcbiAgICAgICAgY29uc3QgZXJyb3JQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhtZXNzYWdlKTtcbiAgICAgICAgaWYgKGVycm9yUHRyKSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuemVyb3Blcmxfc2V0X2hvc3RfZXJyb3IoZXJyb3JQdHIpO1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUoZXJyb3JQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IGludGVnZXIgdmFsdWUuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgdmFsdWUgY3JlYXRpb24gZmFpbHNcbiAgICAgKi9cbiAgICBjcmVhdGVJbnQodmFsdWU6IG51bWJlcik6IFBlcmxWYWx1ZSB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBwdHIgPSB0aGlzLmV4cG9ydHMuemVyb3BlcmxfbmV3X2ludChNYXRoLmZsb29yKHZhbHVlKSk7XG4gICAgICAgIGlmIChwdHIgPT09IDApIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNyZWF0ZSBpbnRlZ2VyIHZhbHVlXCIpO1xuICAgICAgICByZXR1cm4gbmV3IFBlcmxWYWx1ZShwdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IHVuc2lnbmVkIGludGVnZXIgdmFsdWUuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgdmFsdWUgY3JlYXRpb24gZmFpbHNcbiAgICAgKi9cbiAgICBjcmVhdGVVSW50KHZhbHVlOiBudW1iZXIpOiBQZXJsVmFsdWUge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgcHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX25ld191aW50KE1hdGguZmxvb3IoTWF0aC5hYnModmFsdWUpKSk7XG4gICAgICAgIGlmIChwdHIgPT09IDApIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNyZWF0ZSB1bnNpZ25lZCBpbnRlZ2VyIHZhbHVlXCIpO1xuICAgICAgICByZXR1cm4gbmV3IFBlcmxWYWx1ZShwdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IGRvdWJsZS1wcmVjaXNpb24gZmxvYXQgdmFsdWUuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgdmFsdWUgY3JlYXRpb24gZmFpbHNcbiAgICAgKi9cbiAgICBjcmVhdGVEb3VibGUodmFsdWU6IG51bWJlcik6IFBlcmxWYWx1ZSB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBwdHIgPSB0aGlzLmV4cG9ydHMuemVyb3BlcmxfbmV3X2RvdWJsZSh2YWx1ZSk7XG4gICAgICAgIGlmIChwdHIgPT09IDApIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNyZWF0ZSBkb3VibGUgdmFsdWVcIik7XG4gICAgICAgIHJldHVybiBuZXcgUGVybFZhbHVlKHB0ciwgdGhpcy5leHBvcnRzKTtcbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBDcmVhdGUgYSBuZXcgc3RyaW5nIHZhbHVlLlxuICAgICAqIEB0aHJvd3Mge1plcm9QZXJsRXJyb3J9IElmIHZhbHVlIGNyZWF0aW9uIGZhaWxzXG4gICAgICovXG4gICAgY3JlYXRlU3RyaW5nKHZhbHVlOiBzdHJpbmcpOiBQZXJsVmFsdWUge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgYnl0ZXMgPSB0ZXh0RW5jb2Rlci5lbmNvZGUodmFsdWUpO1xuICAgICAgICBjb25zdCBzdHJQdHIgPSB0aGlzLmV4cG9ydHMubWFsbG9jKGJ5dGVzLmxlbmd0aCk7XG4gICAgICAgIG5ldyBVaW50OEFycmF5KHRoaXMuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKS5zZXQoYnl0ZXMsIHN0clB0cik7XG5cbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIGNvbnN0IHZhbFB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9uZXdfc3RyaW5nKHN0clB0ciwgYnl0ZXMubGVuZ3RoKTtcbiAgICAgICAgICAgIGlmICh2YWxQdHIgPT09IDApIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIGNyZWF0ZSBzdHJpbmcgdmFsdWVcIik7XG4gICAgICAgICAgICByZXR1cm4gbmV3IFBlcmxWYWx1ZSh2YWxQdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShzdHJQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IGJvb2xlYW4gdmFsdWUuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgdmFsdWUgY3JlYXRpb24gZmFpbHNcbiAgICAgKi9cbiAgICBjcmVhdGVCb29sKHZhbHVlOiBib29sZWFuKTogUGVybFZhbHVlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9uZXdfYm9vbCh2YWx1ZSA/IDEgOiAwKTtcbiAgICAgICAgaWYgKHB0ciA9PT0gMCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gY3JlYXRlIGJvb2xlYW4gdmFsdWVcIik7XG4gICAgICAgIHJldHVybiBuZXcgUGVybFZhbHVlKHB0ciwgdGhpcy5leHBvcnRzKTtcbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBDcmVhdGUgYSBuZXcgdW5kZWZpbmVkIHZhbHVlLlxuICAgICAqIEB0aHJvd3Mge1plcm9QZXJsRXJyb3J9IElmIHZhbHVlIGNyZWF0aW9uIGZhaWxzXG4gICAgICovXG4gICAgY3JlYXRlVW5kZWYoKTogUGVybFZhbHVlIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9uZXdfdW5kZWYoKTtcbiAgICAgICAgaWYgKHB0ciA9PT0gMCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gY3JlYXRlIHVuZGVmIHZhbHVlXCIpO1xuICAgICAgICByZXR1cm4gbmV3IFBlcmxWYWx1ZShwdHIsIHRoaXMuZXhwb3J0cyk7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IFBlcmwgYXJyYXksIG9wdGlvbmFsbHkgcG9wdWxhdGVkIHdpdGggdmFsdWVzLlxuICAgICAqIEB0aHJvd3Mge1plcm9QZXJsRXJyb3J9IElmIGFycmF5IGNyZWF0aW9uIGZhaWxzXG4gICAgICovXG4gICAgY3JlYXRlQXJyYXkodmFsdWVzPzogUGVybENvbnZlcnRpYmxlW10pOiBQZXJsQXJyYXkge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgcHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX25ld19hcnJheSgpO1xuICAgICAgICBpZiAocHRyID09PSAwKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihcIkZhaWxlZCB0byBjcmVhdGUgYXJyYXlcIik7XG5cbiAgICAgICAgY29uc3QgcGVybEFycmF5ID0gbmV3IFBlcmxBcnJheShwdHIsIHRoaXMuZXhwb3J0cywgdGhpcyk7XG4gICAgICAgIGlmICh2YWx1ZXMpIHtcbiAgICAgICAgICAgIGZvciAoY29uc3QgaXRlbSBvZiB2YWx1ZXMpIHBlcmxBcnJheS5wdXNoKGl0ZW0pO1xuICAgICAgICB9XG4gICAgICAgIHJldHVybiBwZXJsQXJyYXk7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ3JlYXRlIGEgbmV3IFBlcmwgaGFzaCwgb3B0aW9uYWxseSBwb3B1bGF0ZWQgd2l0aCB2YWx1ZXMuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgaGFzaCBjcmVhdGlvbiBmYWlsc1xuICAgICAqL1xuICAgIGNyZWF0ZUhhc2gob2JqZWN0PzogUmVjb3JkPHN0cmluZywgUGVybENvbnZlcnRpYmxlPik6IFBlcmxIYXNoIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IHB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9uZXdfaGFzaCgpO1xuICAgICAgICBpZiAocHRyID09PSAwKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihcIkZhaWxlZCB0byBjcmVhdGUgaGFzaFwiKTtcblxuICAgICAgICBjb25zdCBwZXJsSGFzaCA9IG5ldyBQZXJsSGFzaChwdHIsIHRoaXMuZXhwb3J0cywgdGhpcyk7XG4gICAgICAgIGlmIChvYmplY3QpIHtcbiAgICAgICAgICAgIGZvciAoY29uc3QgW2tleSwgdmFsdWVdIG9mIE9iamVjdC5lbnRyaWVzKG9iamVjdCkpIHBlcmxIYXNoLnNldChrZXksIHZhbHVlKTtcbiAgICAgICAgfVxuICAgICAgICByZXR1cm4gcGVybEhhc2g7XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogQ29udmVydCBhIEphdmFTY3JpcHQgdmFsdWUgdG8gYSBQZXJsVmFsdWUuXG4gICAgICpcbiAgICAgKiBDb252ZXJzaW9uIHJ1bGVzOlxuICAgICAqIC0gUGVybFZhbHVlIOKGkiByZXR1cm5lZCBhcy1pc1xuICAgICAqIC0gbnVsbC91bmRlZmluZWQg4oaSIHVuZGVmXG4gICAgICogLSBib29sZWFuIOKGkiBQZXJsIGJvb2xlYW5cbiAgICAgKiAtIGludGVnZXIg4oaSIFBlcmwgaW50XG4gICAgICogLSBmbG9hdCDihpIgUGVybCBkb3VibGVcbiAgICAgKiAtIHN0cmluZyDihpIgUGVybCBzdHJpbmdcbiAgICAgKiAtIGFycmF5IOKGkiBQZXJsIGFycmF5IHJlZmVyZW5jZVxuICAgICAqIC0gb2JqZWN0IOKGkiBQZXJsIGhhc2ggcmVmZXJlbmNlXG4gICAgICpcbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBjb252ZXJzaW9uIGZhaWxzXG4gICAgICovXG4gICAgdG9QZXJsVmFsdWUodmFsdWU6IFBlcmxDb252ZXJ0aWJsZSk6IFBlcmxWYWx1ZSB7XG4gICAgICAgIGlmICh2YWx1ZSBpbnN0YW5jZW9mIFBlcmxWYWx1ZSkgcmV0dXJuIHZhbHVlO1xuICAgICAgICBpZiAodmFsdWUgPT09IG51bGwgfHwgdmFsdWUgPT09IHVuZGVmaW5lZCkgcmV0dXJuIHRoaXMuY3JlYXRlVW5kZWYoKTtcbiAgICAgICAgaWYgKHR5cGVvZiB2YWx1ZSA9PT0gJ2Jvb2xlYW4nKSByZXR1cm4gdGhpcy5jcmVhdGVCb29sKHZhbHVlKTtcbiAgICAgICAgaWYgKHR5cGVvZiB2YWx1ZSA9PT0gJ251bWJlcicpIHtcbiAgICAgICAgICAgIHJldHVybiBOdW1iZXIuaXNJbnRlZ2VyKHZhbHVlKSA/IHRoaXMuY3JlYXRlSW50KHZhbHVlKSA6IHRoaXMuY3JlYXRlRG91YmxlKHZhbHVlKTtcbiAgICAgICAgfVxuICAgICAgICBpZiAodHlwZW9mIHZhbHVlID09PSAnc3RyaW5nJykgcmV0dXJuIHRoaXMuY3JlYXRlU3RyaW5nKHZhbHVlKTtcbiAgICAgICAgaWYgKEFycmF5LmlzQXJyYXkodmFsdWUpKSB7XG4gICAgICAgICAgICBjb25zdCBhcnIgPSB0aGlzLmNyZWF0ZUFycmF5KHZhbHVlKTtcbiAgICAgICAgICAgIGNvbnN0IHZhbCA9IGFyci50b1ZhbHVlKCk7XG4gICAgICAgICAgICBhcnIuZGlzcG9zZSgpO1xuICAgICAgICAgICAgcmV0dXJuIHZhbDtcbiAgICAgICAgfVxuICAgICAgICBpZiAodHlwZW9mIHZhbHVlID09PSAnb2JqZWN0Jykge1xuICAgICAgICAgICAgY29uc3QgaGFzaCA9IHRoaXMuY3JlYXRlSGFzaCh2YWx1ZSk7XG4gICAgICAgICAgICBjb25zdCB2YWwgPSBoYXNoLnRvVmFsdWUoKTtcbiAgICAgICAgICAgIGhhc2guZGlzcG9zZSgpO1xuICAgICAgICAgICAgcmV0dXJuIHZhbDtcbiAgICAgICAgfVxuICAgICAgICB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihgQ2Fubm90IGNvbnZlcnQgdmFsdWUgb2YgdHlwZSAke3R5cGVvZiB2YWx1ZX0gdG8gUGVybFZhbHVlYCk7XG4gICAgfVxuXG4gICAgLyoqIEdldCBhIGdsb2JhbCBzY2FsYXIgdmFyaWFibGUuIFJldHVybnMgbnVsbCBpZiB2YXJpYWJsZSBkb2Vzbid0IGV4aXN0LiAqL1xuICAgIGdldFZhcmlhYmxlKG5hbWU6IHN0cmluZyk6IFBlcmxWYWx1ZSB8IG51bGwge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgbmFtZVB0ciA9IHRoaXMud3JpdGVDU3RyaW5nKG5hbWUpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgdmFsUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2dldF92YXIobmFtZVB0cik7XG4gICAgICAgICAgICByZXR1cm4gdmFsUHRyID09PSAwID8gbnVsbCA6IG5ldyBQZXJsVmFsdWUodmFsUHRyLCB0aGlzLmV4cG9ydHMpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUobmFtZVB0cik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKiogR2V0IGEgZ2xvYmFsIGFycmF5IHZhcmlhYmxlLiBSZXR1cm5zIG51bGwgaWYgdmFyaWFibGUgZG9lc24ndCBleGlzdC4gKi9cbiAgICBnZXRBcnJheVZhcmlhYmxlKG5hbWU6IHN0cmluZyk6IFBlcmxBcnJheSB8IG51bGwge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgbmFtZVB0ciA9IHRoaXMud3JpdGVDU3RyaW5nKG5hbWUpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgYXJyUHRyID0gdGhpcy5leHBvcnRzLnplcm9wZXJsX2dldF9hcnJheV92YXIobmFtZVB0cik7XG4gICAgICAgICAgICByZXR1cm4gYXJyUHRyID09PSAwID8gbnVsbCA6IG5ldyBQZXJsQXJyYXkoYXJyUHRyLCB0aGlzLmV4cG9ydHMsIHRoaXMpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUobmFtZVB0cik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKiogR2V0IGEgZ2xvYmFsIGhhc2ggdmFyaWFibGUuIFJldHVybnMgbnVsbCBpZiB2YXJpYWJsZSBkb2Vzbid0IGV4aXN0LiAqL1xuICAgIGdldEhhc2hWYXJpYWJsZShuYW1lOiBzdHJpbmcpOiBQZXJsSGFzaCB8IG51bGwge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgbmFtZVB0ciA9IHRoaXMud3JpdGVDU3RyaW5nKG5hbWUpO1xuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgaGFzaFB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9nZXRfaGFzaF92YXIobmFtZVB0cik7XG4gICAgICAgICAgICByZXR1cm4gaGFzaFB0ciA9PT0gMCA/IG51bGwgOiBuZXcgUGVybEhhc2goaGFzaFB0ciwgdGhpcy5leHBvcnRzLCB0aGlzKTtcbiAgICAgICAgfSBmaW5hbGx5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKG5hbWVQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogU2V0IGEgZ2xvYmFsIHNjYWxhciB2YXJpYWJsZS5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiBzZXR0aW5nIHRoZSB2YXJpYWJsZSBmYWlsc1xuICAgICAqL1xuICAgIHNldFZhcmlhYmxlKG5hbWU6IHN0cmluZywgdmFsdWU6IFBlcmxDb252ZXJ0aWJsZSk6IHZvaWQge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgcGVybFZhbHVlID0gdGhpcy50b1BlcmxWYWx1ZSh2YWx1ZSk7XG4gICAgICAgIGNvbnN0IG5hbWVQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhuYW1lKTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIGlmICghdGhpcy5leHBvcnRzLnplcm9wZXJsX3NldF92YXIobmFtZVB0ciwgcGVybFZhbHVlLmdldFB0cigpKSkge1xuICAgICAgICAgICAgICAgIHRocm93IG5ldyBaZXJvUGVybEVycm9yKGBGYWlsZWQgdG8gc2V0IHZhcmlhYmxlICcke25hbWV9J2ApO1xuICAgICAgICAgICAgfVxuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUobmFtZVB0cik7XG4gICAgICAgICAgICBpZiAoISh2YWx1ZSBpbnN0YW5jZW9mIFBlcmxWYWx1ZSkpIHBlcmxWYWx1ZS5kaXNwb3NlKCk7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBSZWdpc3RlciBhIEphdmFTY3JpcHQgZnVuY3Rpb24gdGhhdCBjYW4gYmUgY2FsbGVkIGZyb20gUGVybC5cbiAgICAgKiBUaGUgZnVuY3Rpb24gcmVjZWl2ZXMgUGVybCB2YWx1ZXMgYXMgYXJndW1lbnRzIGFuZCByZXR1cm5zIGEgUGVybCB2YWx1ZSBvciB2b2lkLlxuICAgICAqXG4gICAgICogQGV4YW1wbGVcbiAgICAgKiBgYGB0eXBlc2NyaXB0XG4gICAgICogcGVybC5yZWdpc3RlckZ1bmN0aW9uKCdhZGQnLCAoYSwgYikgPT4ge1xuICAgICAqICAgcmV0dXJuIHBlcmwuY3JlYXRlSW50KGEudG9JbnQoKSArIGIudG9JbnQoKSk7XG4gICAgICogfSk7XG4gICAgICogYXdhaXQgcGVybC5ldmFsKCdwcmludCBhZGQoMTAsIDMyKSwgXCJcXG5cIicpOyAvLyBwcmludHM6IDQyXG4gICAgICogYGBgXG4gICAgICovXG4gICAgcmVnaXN0ZXJGdW5jdGlvbihuYW1lOiBzdHJpbmcsIGZuOiBIb3N0RnVuY3Rpb24pOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGNvbnN0IGZ1bmNJZCA9IHRoaXMubmV4dEZ1bmNJZCsrO1xuICAgICAgICB0aGlzLmhvc3RGdW5jdGlvbnMuc2V0KGZ1bmNJZCwgZm4pO1xuXG4gICAgICAgIGNvbnN0IG5hbWVQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhuYW1lKTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9yZWdpc3Rlcl9mdW5jdGlvbihmdW5jSWQsIG5hbWVQdHIpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUobmFtZVB0cik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBSZWdpc3RlciBhIEphdmFTY3JpcHQgbWV0aG9kIHRoYXQgY2FuIGJlIGNhbGxlZCBmcm9tIFBlcmwuXG4gICAgICpcbiAgICAgKiBAZXhhbXBsZVxuICAgICAqIGBgYHR5cGVzY3JpcHRcbiAgICAgKiBwZXJsLnJlZ2lzdGVyTWV0aG9kKCdNYXRoJywgJ3NxdWFyZScsICh4KSA9PiB7XG4gICAgICogICBjb25zdCBudW0gPSB4LnRvSW50KCk7XG4gICAgICogICByZXR1cm4gcGVybC5jcmVhdGVJbnQobnVtICogbnVtKTtcbiAgICAgKiB9KTtcbiAgICAgKiBhd2FpdCBwZXJsLmV2YWwoJyRyZXN1bHQgPSBNYXRoOjpzcXVhcmUoNyknKTsgLy8gJHJlc3VsdCA9IDQ5XG4gICAgICogYGBgXG4gICAgICovXG4gICAgcmVnaXN0ZXJNZXRob2QocGFja2FnZU5hbWU6IHN0cmluZywgbWV0aG9kTmFtZTogc3RyaW5nLCBmbjogSG9zdEZ1bmN0aW9uKTogdm9pZCB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICBjb25zdCBmdW5jSWQgPSB0aGlzLm5leHRGdW5jSWQrKztcbiAgICAgICAgdGhpcy5ob3N0RnVuY3Rpb25zLnNldChmdW5jSWQsIGZuKTtcblxuICAgICAgICBjb25zdCBwa2dQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhwYWNrYWdlTmFtZSk7XG4gICAgICAgIGNvbnN0IG1ldGhQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhtZXRob2ROYW1lKTtcbiAgICAgICAgdHJ5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9yZWdpc3Rlcl9tZXRob2QoZnVuY0lkLCBwa2dQdHIsIG1ldGhQdHIpO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUocGtnUHRyKTtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKG1ldGhQdHIpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqIENhbGwgYSBQZXJsIHN1YnJvdXRpbmUgaW4gdm9pZCBjb250ZXh0LiAqL1xuICAgIGNhbGwobmFtZTogc3RyaW5nLCBhcmdzOiBQZXJsVmFsdWVbXSwgY29udGV4dDogXCJ2b2lkXCIpOiBQcm9taXNlPHVuZGVmaW5lZD47XG4gICAgLyoqIENhbGwgYSBQZXJsIHN1YnJvdXRpbmUgaW4gc2NhbGFyIGNvbnRleHQuICovXG4gICAgY2FsbChuYW1lOiBzdHJpbmcsIGFyZ3M6IFBlcmxWYWx1ZVtdLCBjb250ZXh0OiBcInNjYWxhclwiKTogUHJvbWlzZTxQZXJsVmFsdWUgfCBudWxsPjtcbiAgICAvKiogQ2FsbCBhIFBlcmwgc3Vicm91dGluZSBpbiBsaXN0IGNvbnRleHQuICovXG4gICAgY2FsbChuYW1lOiBzdHJpbmcsIGFyZ3M6IFBlcmxWYWx1ZVtdLCBjb250ZXh0OiBcImxpc3RcIik6IFByb21pc2U8UGVybFZhbHVlW10+O1xuICAgIC8qKiBDYWxsIGEgUGVybCBzdWJyb3V0aW5lIChkZWZhdWx0cyB0byBzY2FsYXIgY29udGV4dCkuICovXG4gICAgY2FsbChuYW1lOiBzdHJpbmcsIGFyZ3M/OiBQZXJsVmFsdWVbXSwgY29udGV4dD86IFBlcmxDb250ZXh0KTogUHJvbWlzZTxQZXJsVmFsdWUgfCBudWxsPjtcblxuICAgIGFzeW5jIGNhbGwoXG4gICAgICAgIG5hbWU6IHN0cmluZyxcbiAgICAgICAgYXJnczogUGVybFZhbHVlW10gPSBbXSxcbiAgICAgICAgY29udGV4dDogUGVybENvbnRleHQgPSBcInNjYWxhclwiLFxuICAgICk6IFByb21pc2U8dW5kZWZpbmVkIHwgUGVybFZhbHVlIHwgbnVsbCB8IFBlcmxWYWx1ZVtdPiB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuXG4gICAgICAgIGNvbnN0IG5hbWVQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhuYW1lKTtcbiAgICAgICAgY29uc3QgY29udGV4dE51bSA9IG1hcENvbnRleHQoY29udGV4dCk7XG4gICAgICAgIGxldCBhcmd2UHRyID0gMDtcblxuICAgICAgICBpZiAoYXJncy5sZW5ndGggPiAwKSB7XG4gICAgICAgICAgICBhcmd2UHRyID0gdGhpcy5leHBvcnRzLm1hbGxvYyhhcmdzLmxlbmd0aCAqIDQpO1xuICAgICAgICAgICAgY29uc3QgdmlldyA9IG5ldyBEYXRhVmlldyh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcik7XG4gICAgICAgICAgICBmb3IgKGxldCBpID0gMDsgaSA8IGFyZ3MubGVuZ3RoOyBpKyspIHtcbiAgICAgICAgICAgICAgICBjb25zdCBhcmcgPSBhcmdzW2ldO1xuICAgICAgICAgICAgICAgIGlmICghYXJnKSB0aHJvdyBuZXcgWmVyb1BlcmxFcnJvcihgQXJndW1lbnQgYXQgaW5kZXggJHtpfSBpcyB1bmRlZmluZWRgKTtcbiAgICAgICAgICAgICAgICB2aWV3LnNldFVpbnQzMihhcmd2UHRyICsgaSAqIDQsIGFyZy5nZXRQdHIoKSwgdHJ1ZSk7XG4gICAgICAgICAgICB9XG4gICAgICAgIH1cblxuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgcmVzdWx0UHRyID0gYXdhaXQgdGhpcy5leHBvcnRzLnplcm9wZXJsX2NhbGwobmFtZVB0ciwgY29udGV4dE51bSwgYXJncy5sZW5ndGgsIGFyZ3ZQdHIpO1xuXG4gICAgICAgICAgICBpZiAocmVzdWx0UHRyID09PSAwKSB7XG4gICAgICAgICAgICAgICAgaWYgKGNvbnRleHQgPT09IFwidm9pZFwiKSByZXR1cm47XG4gICAgICAgICAgICAgICAgaWYgKGNvbnRleHQgPT09IFwic2NhbGFyXCIpIHJldHVybiBudWxsO1xuICAgICAgICAgICAgICAgIHJldHVybiBbXTtcbiAgICAgICAgICAgIH1cblxuICAgICAgICAgICAgY29uc3QgdmlldyA9IG5ldyBEYXRhVmlldyh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcik7XG4gICAgICAgICAgICBjb25zdCBjb3VudCA9IHZpZXcuZ2V0SW50MzIocmVzdWx0UHRyLCB0cnVlKTtcblxuICAgICAgICAgICAgY29uc3QgcmVzdWx0czogUGVybFZhbHVlW10gPSBbXTtcbiAgICAgICAgICAgIGZvciAobGV0IGkgPSAwOyBpIDwgY291bnQ7IGkrKykge1xuICAgICAgICAgICAgICAgIGNvbnN0IHZhbFB0ciA9IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9yZXN1bHRfZ2V0KHJlc3VsdFB0ciwgaSk7XG4gICAgICAgICAgICAgICAgaWYgKHZhbFB0ciAhPT0gMCkgcmVzdWx0cy5wdXNoKG5ldyBQZXJsVmFsdWUodmFsUHRyLCB0aGlzLmV4cG9ydHMpKTtcbiAgICAgICAgICAgIH1cblxuICAgICAgICAgICAgY29uc3QgdmFsdWVzQXJyYXlQdHIgPSB2aWV3LmdldFVpbnQzMihyZXN1bHRQdHIgKyA0LCB0cnVlKTtcbiAgICAgICAgICAgIGlmICh2YWx1ZXNBcnJheVB0ciAhPT0gMCkgdGhpcy5leHBvcnRzLmZyZWUodmFsdWVzQXJyYXlQdHIpO1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUocmVzdWx0UHRyKTtcblxuICAgICAgICAgICAgaWYgKGNvbnRleHQgPT09IFwidm9pZFwiKSB7XG4gICAgICAgICAgICAgICAgZm9yIChjb25zdCB2YWwgb2YgcmVzdWx0cykgdmFsLmRpc3Bvc2UoKTtcbiAgICAgICAgICAgICAgICByZXR1cm47XG4gICAgICAgICAgICB9XG4gICAgICAgICAgICBpZiAoY29udGV4dCA9PT0gXCJzY2FsYXJcIikgcmV0dXJuIHJlc3VsdHNbMF0gPz8gbnVsbDtcbiAgICAgICAgICAgIHJldHVybiByZXN1bHRzO1xuICAgICAgICB9IGNhdGNoIChlKSB7XG4gICAgICAgICAgICBpZiAoZSBpbnN0YW5jZW9mIFdBU0lQcm9jRXhpdCkge1xuICAgICAgICAgICAgICAgIGlmIChjb250ZXh0ID09PSBcInZvaWRcIikgcmV0dXJuO1xuICAgICAgICAgICAgICAgIGlmIChjb250ZXh0ID09PSBcInNjYWxhclwiKSByZXR1cm4gbnVsbDtcbiAgICAgICAgICAgICAgICByZXR1cm4gW107XG4gICAgICAgICAgICB9XG4gICAgICAgICAgICB0aHJvdyBlO1xuICAgICAgICB9IGZpbmFsbHkge1xuICAgICAgICAgICAgdGhpcy5leHBvcnRzLmZyZWUobmFtZVB0cik7XG4gICAgICAgICAgICBpZiAoYXJndlB0ciAhPT0gMCkgdGhpcy5leHBvcnRzLmZyZWUoYXJndlB0cik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBFdmFsdWF0ZSBhIHN0cmluZyBvZiBQZXJsIGNvZGUuXG4gICAgICogQHBhcmFtIGNvZGUgUGVybCBjb2RlIHRvIGV2YWx1YXRlXG4gICAgICogQHBhcmFtIGFyZ3MgQXJndW1lbnRzIHRvIHBhc3MgYXMgQEFSR1ZcbiAgICAgKi9cbiAgICBhc3luYyBldmFsKGNvZGU6IHN0cmluZywgYXJnczogc3RyaW5nW10gPSBbXSk6IFByb21pc2U8WmVyb1BlcmxSZXN1bHQ+IHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG5cbiAgICAgICAgY29uc3QgY29kZVB0ciA9IHRoaXMud3JpdGVDU3RyaW5nKGNvZGUpO1xuICAgICAgICBsZXQgYXJndiA9IDA7XG4gICAgICAgIGxldCBidWZmZXJzOiBudW1iZXJbXSA9IFtdO1xuXG4gICAgICAgIGlmIChhcmdzLmxlbmd0aCA+IDApIHtcbiAgICAgICAgICAgIGNvbnN0IHJlc3VsdCA9IHRoaXMud3JpdGVTdHJpbmdBcnJheShhcmdzKTtcbiAgICAgICAgICAgIGFyZ3YgPSByZXN1bHQuYXJndjtcbiAgICAgICAgICAgIGJ1ZmZlcnMgPSByZXN1bHQuYnVmZmVycztcbiAgICAgICAgfVxuXG4gICAgICAgIHRyeSB7XG4gICAgICAgICAgICBjb25zdCBleGl0Q29kZSA9IGF3YWl0IHRoaXMuZXhwb3J0cy56ZXJvcGVybF9ldmFsKGNvZGVQdHIsIG1hcENvbnRleHQoXCJzY2FsYXJcIiksIGFyZ3MubGVuZ3RoLCBhcmd2KTtcbiAgICAgICAgICAgIGlmIChleGl0Q29kZSAhPT0gMCkge1xuICAgICAgICAgICAgICAgIHJldHVybiB7IHN1Y2Nlc3M6IGZhbHNlLCBlcnJvcjogdGhpcy5nZXRMYXN0RXJyb3IoKSwgZXhpdENvZGUgfTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgICAgIHJldHVybiB7IHN1Y2Nlc3M6IHRydWUsIGV4aXRDb2RlOiAwIH07XG4gICAgICAgIH0gY2F0Y2ggKGUpIHtcbiAgICAgICAgICAgIGlmIChlIGluc3RhbmNlb2YgV0FTSVByb2NFeGl0KSB7XG4gICAgICAgICAgICAgICAgaWYgKGUuY29kZSAhPT0gMCkge1xuICAgICAgICAgICAgICAgICAgICByZXR1cm4geyBzdWNjZXNzOiBmYWxzZSwgZXJyb3I6IHRoaXMuZ2V0TGFzdEVycm9yKCksIGV4aXRDb2RlOiBlLmNvZGUgfTtcbiAgICAgICAgICAgICAgICB9XG4gICAgICAgICAgICAgICAgcmV0dXJuIHsgc3VjY2VzczogdHJ1ZSwgZXhpdENvZGU6IDAgfTtcbiAgICAgICAgICAgIH1cbiAgICAgICAgICAgIHRocm93IGU7XG4gICAgICAgIH0gZmluYWxseSB7XG4gICAgICAgICAgICB0aGlzLmV4cG9ydHMuZnJlZShjb2RlUHRyKTtcbiAgICAgICAgICAgIGlmIChidWZmZXJzLmxlbmd0aCA+IDApIHRoaXMuZnJlZVN0cmluZ0FycmF5KGFyZ3YsIGJ1ZmZlcnMpO1xuICAgICAgICB9XG4gICAgfVxuXG4gICAgLyoqXG4gICAgICogUnVuIGEgUGVybCBzY3JpcHQgZmlsZS5cbiAgICAgKiBAcGFyYW0gc2NyaXB0UGF0aCBQYXRoIHRvIHRoZSBzY3JpcHQgZmlsZVxuICAgICAqIEBwYXJhbSBhcmdzIEFyZ3VtZW50cyB0byBwYXNzIGFzIEBBUkdWXG4gICAgICovXG4gICAgYXN5bmMgcnVuRmlsZShzY3JpcHRQYXRoOiBzdHJpbmcsIGFyZ3M6IHN0cmluZ1tdID0gW10pOiBQcm9taXNlPFplcm9QZXJsUmVzdWx0PiB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuXG4gICAgICAgIGNvbnN0IHBhdGhQdHIgPSB0aGlzLndyaXRlQ1N0cmluZyhzY3JpcHRQYXRoKTtcbiAgICAgICAgbGV0IGFyZ3YgPSAwO1xuICAgICAgICBsZXQgYnVmZmVyczogbnVtYmVyW10gPSBbXTtcblxuICAgICAgICBpZiAoYXJncy5sZW5ndGggPiAwKSB7XG4gICAgICAgICAgICBjb25zdCByZXN1bHQgPSB0aGlzLndyaXRlU3RyaW5nQXJyYXkoYXJncyk7XG4gICAgICAgICAgICBhcmd2ID0gcmVzdWx0LmFyZ3Y7XG4gICAgICAgICAgICBidWZmZXJzID0gcmVzdWx0LmJ1ZmZlcnM7XG4gICAgICAgIH1cblxuICAgICAgICB0cnkge1xuICAgICAgICAgICAgY29uc3QgZXhpdENvZGUgPSBhd2FpdCB0aGlzLmV4cG9ydHMuemVyb3BlcmxfcnVuX2ZpbGUocGF0aFB0ciwgYXJncy5sZW5ndGgsIGFyZ3YpO1xuICAgICAgICAgICAgaWYgKGV4aXRDb2RlICE9PSAwKSB7XG4gICAgICAgICAgICAgICAgcmV0dXJuIHsgc3VjY2VzczogZmFsc2UsIGVycm9yOiB0aGlzLmdldExhc3RFcnJvcigpLCBleGl0Q29kZSB9O1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgcmV0dXJuIHsgc3VjY2VzczogdHJ1ZSwgZXhpdENvZGU6IDAgfTtcbiAgICAgICAgfSBjYXRjaCAoZSkge1xuICAgICAgICAgICAgaWYgKGUgaW5zdGFuY2VvZiBXQVNJUHJvY0V4aXQpIHtcbiAgICAgICAgICAgICAgICBpZiAoZS5jb2RlICE9PSAwKSB7XG4gICAgICAgICAgICAgICAgICAgIHJldHVybiB7IHN1Y2Nlc3M6IGZhbHNlLCBlcnJvcjogdGhpcy5nZXRMYXN0RXJyb3IoKSwgZXhpdENvZGU6IGUuY29kZSB9O1xuICAgICAgICAgICAgICAgIH1cbiAgICAgICAgICAgICAgICByZXR1cm4geyBzdWNjZXNzOiB0cnVlLCBleGl0Q29kZTogMCB9O1xuICAgICAgICAgICAgfVxuICAgICAgICAgICAgdGhyb3cgZTtcbiAgICAgICAgfSBmaW5hbGx5IHtcbiAgICAgICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKHBhdGhQdHIpO1xuICAgICAgICAgICAgaWYgKGJ1ZmZlcnMubGVuZ3RoID4gMCkgdGhpcy5mcmVlU3RyaW5nQXJyYXkoYXJndiwgYnVmZmVycyk7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKipcbiAgICAgKiBSZXNldCB0aGUgaW50ZXJwcmV0ZXIgdG8gYSBjbGVhbiBzdGF0ZS5cbiAgICAgKiBDbGVhcnMgYWxsIHZhcmlhYmxlcyBhbmQgZXJyb3JzLiBSZWdpc3RlcmVkIGhvc3QgZnVuY3Rpb25zIHJlbWFpbi5cbiAgICAgKiBAdGhyb3dzIHtaZXJvUGVybEVycm9yfSBJZiByZXNldCBmYWlsc1xuICAgICAqL1xuICAgIGFzeW5jIHJlc2V0KCk6IFByb21pc2U8dm9pZD4ge1xuICAgICAgICB0aGlzLmNoZWNrRGlzcG9zZWQoKTtcbiAgICAgICAgY29uc3QgcmVzdWx0ID0gYXdhaXQgdGhpcy5leHBvcnRzLnplcm9wZXJsX3Jlc2V0KCk7XG4gICAgICAgIGlmIChyZXN1bHQgIT09IDApIHtcbiAgICAgICAgICAgIHRocm93IG5ldyBaZXJvUGVybEVycm9yKFwiRmFpbGVkIHRvIHJlc2V0IFBlcmwgaW50ZXJwcmV0ZXJcIiwgcmVzdWx0LCB0aGlzLmdldExhc3RFcnJvcigpKTtcbiAgICAgICAgfVxuICAgIH1cblxuICAgIC8qKlxuICAgICAqIEZsdXNoIFNURE9VVCBhbmQgU1RERVJSIGJ1ZmZlcnMuXG4gICAgICogQHRocm93cyB7WmVyb1BlcmxFcnJvcn0gSWYgZmx1c2ggZmFpbHNcbiAgICAgKi9cbiAgICBmbHVzaCgpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIGlmICh0aGlzLmV4cG9ydHMuemVyb3BlcmxfZmx1c2goKSAhPT0gMCkge1xuICAgICAgICAgICAgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJGYWlsZWQgdG8gZmx1c2ggb3V0cHV0IGJ1ZmZlcnNcIik7XG4gICAgICAgIH1cbiAgICB9XG5cbiAgICAvKiogR2V0IHRoZSBsYXN0IGVycm9yIG1lc3NhZ2UgZnJvbSBQZXJsICgkQCkuICovXG4gICAgZ2V0TGFzdEVycm9yKCk6IHN0cmluZyB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICByZXR1cm4gdGhpcy5yZWFkQ1N0cmluZyh0aGlzLmV4cG9ydHMuemVyb3BlcmxfbGFzdF9lcnJvcigpKTtcbiAgICB9XG5cbiAgICAvKiogQ2xlYXIgdGhlIGVycm9yIHN0YXRlICgkQCkuICovXG4gICAgY2xlYXJFcnJvcigpOiB2b2lkIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHRoaXMuZXhwb3J0cy56ZXJvcGVybF9jbGVhcl9lcnJvcigpO1xuICAgIH1cblxuICAgIC8qKiBDaGVjayBpZiB0aGUgaW50ZXJwcmV0ZXIgaXMgaW5pdGlhbGl6ZWQuICovXG4gICAgaXNJbml0aWFsaXplZCgpOiBib29sZWFuIHtcbiAgICAgICAgdGhpcy5jaGVja0Rpc3Bvc2VkKCk7XG4gICAgICAgIHJldHVybiB0aGlzLmV4cG9ydHMuemVyb3BlcmxfaXNfaW5pdGlhbGl6ZWQoKSAhPT0gMDtcbiAgICB9XG5cbiAgICAvKiogQ2hlY2sgaWYgdGhlIGludGVycHJldGVyIGlzIHJlYWR5IHRvIGV2YWx1YXRlIGNvZGUuICovXG4gICAgY2FuRXZhbHVhdGUoKTogYm9vbGVhbiB7XG4gICAgICAgIHRoaXMuY2hlY2tEaXNwb3NlZCgpO1xuICAgICAgICByZXR1cm4gdGhpcy5leHBvcnRzLnplcm9wZXJsX2Nhbl9ldmFsdWF0ZSgpICE9PSAwO1xuICAgIH1cblxuICAgIC8qKiBGcmVlIHRoZSBQZXJsIGludGVycHJldGVyJ3MgbWVtb3J5LiBBZnRlciBjYWxsaW5nLCB0aGlzIGluc3RhbmNlIGNhbm5vdCBiZSB1c2VkLiAqL1xuICAgIGRpc3Bvc2UoKTogdm9pZCB7XG4gICAgICAgIGlmICh0aGlzLmlzRGlzcG9zZWQpIHJldHVybjtcbiAgICAgICAgdGhpcy5leHBvcnRzLnplcm9wZXJsX2ZyZWVfaW50ZXJwcmV0ZXIoKTtcbiAgICAgICAgdGhpcy5pc0Rpc3Bvc2VkID0gdHJ1ZTtcbiAgICAgICAgdGhpcy5ob3N0RnVuY3Rpb25zLmNsZWFyKCk7XG4gICAgfVxuXG4gICAgLyoqIFNodXQgZG93biB0aGUgUGVybCBzeXN0ZW0uIEFmdGVyIGNhbGxpbmcsIHRoaXMgaW5zdGFuY2UgY2Fubm90IGJlIHVzZWQuICovXG4gICAgc2h1dGRvd24oKTogdm9pZCB7XG4gICAgICAgIGlmICh0aGlzLmlzRGlzcG9zZWQpIHJldHVybjtcbiAgICAgICAgdGhpcy5leHBvcnRzLnplcm9wZXJsX3NodXRkb3duKCk7XG4gICAgICAgIHRoaXMuaXNEaXNwb3NlZCA9IHRydWU7XG4gICAgICAgIHRoaXMuaG9zdEZ1bmN0aW9ucy5jbGVhcigpO1xuICAgIH1cblxuICAgIHByaXZhdGUgd3JpdGVDU3RyaW5nKHN0cjogc3RyaW5nKTogbnVtYmVyIHtcbiAgICAgICAgaWYgKCFzdHIpIHtcbiAgICAgICAgICAgIHJldHVybiAwO1xuICAgICAgICB9XG4gICAgICAgIGNvbnN0IGJ5dGVzID0gdGV4dEVuY29kZXIuZW5jb2RlKGAke3N0cn1cXDBgKTtcbiAgICAgICAgY29uc3QgcHRyID0gdGhpcy5leHBvcnRzLm1hbGxvYyhieXRlcy5sZW5ndGgpO1xuICAgICAgICBuZXcgVWludDhBcnJheSh0aGlzLmV4cG9ydHMubWVtb3J5LmJ1ZmZlcikuc2V0KGJ5dGVzLCBwdHIpO1xuICAgICAgICByZXR1cm4gcHRyO1xuICAgIH1cblxuICAgIHByaXZhdGUgcmVhZENTdHJpbmcocHRyOiBudW1iZXIpOiBzdHJpbmcge1xuICAgICAgICBpZiAocHRyID09PSAwKSByZXR1cm4gXCJcIjtcbiAgICAgICAgY29uc3QgdmlldyA9IG5ldyBVaW50OEFycmF5KHRoaXMuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKTtcbiAgICAgICAgbGV0IGxlbiA9IDA7XG4gICAgICAgIHdoaWxlICh2aWV3W3B0ciArIGxlbl0gIT09IDApIGxlbisrO1xuICAgICAgICByZXR1cm4gdGV4dERlY29kZXIuZGVjb2RlKHZpZXcuc3ViYXJyYXkocHRyLCBwdHIgKyBsZW4pKTtcbiAgICB9XG5cbiAgICBwcml2YXRlIHdyaXRlU3RyaW5nQXJyYXkoYXJnczogc3RyaW5nW10pOiB7IGFyZ3Y6IG51bWJlcjsgYnVmZmVyczogbnVtYmVyW10gfSB7XG4gICAgICAgIGNvbnN0IGJ1ZmZlcnM6IG51bWJlcltdID0gW107XG4gICAgICAgIGNvbnN0IGFyZ3YgPSB0aGlzLmV4cG9ydHMubWFsbG9jKGFyZ3MubGVuZ3RoICogNCk7XG4gICAgICAgIGNvbnN0IGFyZ3ZWaWV3ID0gbmV3IERhdGFWaWV3KHRoaXMuZXhwb3J0cy5tZW1vcnkuYnVmZmVyKTtcblxuICAgICAgICBmb3IgKGxldCBpID0gMDsgaSA8IGFyZ3MubGVuZ3RoOyBpKyspIHtcbiAgICAgICAgICAgIGNvbnN0IGFyZyA9IGFyZ3NbaV07XG4gICAgICAgICAgICBpZiAoYXJnID09PSB1bmRlZmluZWQpIHRocm93IG5ldyBaZXJvUGVybEVycm9yKGBBcmd1bWVudCBhdCBpbmRleCAke2l9IGlzIHVuZGVmaW5lZGApO1xuICAgICAgICAgICAgY29uc3Qgc3RyUHRyID0gdGhpcy53cml0ZUNTdHJpbmcoYXJnKTtcbiAgICAgICAgICAgIGJ1ZmZlcnMucHVzaChzdHJQdHIpO1xuICAgICAgICAgICAgYXJndlZpZXcuc2V0VWludDMyKGFyZ3YgKyBpICogNCwgc3RyUHRyLCB0cnVlKTtcbiAgICAgICAgfVxuXG4gICAgICAgIHJldHVybiB7IGFyZ3YsIGJ1ZmZlcnMgfTtcbiAgICB9XG5cbiAgICBwcml2YXRlIGZyZWVTdHJpbmdBcnJheShhcmd2OiBudW1iZXIsIGJ1ZmZlcnM6IG51bWJlcltdKTogdm9pZCB7XG4gICAgICAgIGZvciAoY29uc3QgYnVmIG9mIGJ1ZmZlcnMpIHRoaXMuZXhwb3J0cy5mcmVlKGJ1Zik7XG4gICAgICAgIHRoaXMuZXhwb3J0cy5mcmVlKGFyZ3YpO1xuICAgIH1cblxuICAgIHByaXZhdGUgY2hlY2tEaXNwb3NlZCgpOiB2b2lkIHtcbiAgICAgICAgaWYgKHRoaXMuaXNEaXNwb3NlZCkgdGhyb3cgbmV3IFplcm9QZXJsRXJyb3IoXCJaZXJvUGVybCBpbnN0YW5jZSBoYXMgYmVlbiBkaXNwb3NlZFwiKTtcbiAgICB9XG59XG4iLAogICAgIi8qKlxuICogQSBsaWdodHdlaWdodCBTdHJpbmdCdWlsZGVyXG4gKi9cbmV4cG9ydCBjbGFzcyBTdHJpbmdCdWlsZGVyIHtcbiAgcHJpdmF0ZSBwYXJ0czogc3RyaW5nW107XG5cbiAgLyoqXG4gICAqIENyZWF0ZXMgYSBuZXcgU3RyaW5nQnVpbGRlciBpbnN0YW5jZVxuICAgKiBAcGFyYW0gaW5pdGlhbFZhbHVlIE9wdGlvbmFsIGluaXRpYWwgc3RyaW5nIHZhbHVlXG4gICAqL1xuICBjb25zdHJ1Y3Rvcihpbml0aWFsVmFsdWUgPSBcIlwiKSB7XG4gICAgdGhpcy5wYXJ0cyA9IGluaXRpYWxWYWx1ZSA/IFtpbml0aWFsVmFsdWVdIDogW107XG4gIH1cblxuICAvKipcbiAgICogQXBwZW5kcyBhIHN0cmluZyB0byB0aGUgYnVpbGRlclxuICAgKiBAcGFyYW0gc3RyIFRoZSBzdHJpbmcgdG8gYXBwZW5kXG4gICAqIEByZXR1cm5zIFRoZSBTdHJpbmdCdWlsZGVyIGluc3RhbmNlIGZvciBjaGFpbmluZ1xuICAgKi9cbiAgYXBwZW5kKHN0cjogc3RyaW5nKTogU3RyaW5nQnVpbGRlciB7XG4gICAgdGhpcy5wYXJ0cy5wdXNoKHN0cik7XG4gICAgcmV0dXJuIHRoaXM7XG4gIH1cblxuICAvKipcbiAqIENsZWFycyBhbGwgY29udGVudCBmcm9tIHRoZSBidWlsZGVyXG4gKiBAcmV0dXJucyBUaGUgU3RyaW5nQnVpbGRlciBpbnN0YW5jZSBmb3IgY2hhaW5pbmdcbiAqL1xuICBjbGVhcigpOiBTdHJpbmdCdWlsZGVyIHtcbiAgICB0aGlzLnBhcnRzID0gW107XG4gICAgcmV0dXJuIHRoaXM7XG4gIH1cblxuICAvKipcbiAgICogQXBwZW5kcyBhIHN0cmluZyBmb2xsb3dlZCBieSBhIG5ld2xpbmUgY2hhcmFjdGVyXG4gICAqIEBwYXJhbSBzdHIgVGhlIHN0cmluZyB0byBhcHBlbmRcbiAgICogQHJldHVybnMgVGhlIFN0cmluZ0J1aWxkZXIgaW5zdGFuY2UgZm9yIGNoYWluaW5nXG4gICAqL1xuICBhcHBlbmRMaW5lKHN0ciA9IFwiXCIpOiBTdHJpbmdCdWlsZGVyIHtcbiAgICB0aGlzLnBhcnRzLnB1c2goYCR7c3RyfVxcbmApO1xuICAgIHJldHVybiB0aGlzO1xuICB9XG5cbiAgLyoqXG4gICAqIFJldHVybnMgdGhlIGN1cnJlbnQgbGVuZ3RoIG9mIHRoZSBzdHJpbmdcbiAgICovXG4gIGdldCBsZW5ndGgoKTogbnVtYmVyIHtcbiAgICByZXR1cm4gdGhpcy50b1N0cmluZygpLmxlbmd0aDtcbiAgfVxuXG4gIC8qKlxuICAgKiBDb252ZXJ0cyB0aGUgU3RyaW5nQnVpbGRlciB0byBhIHN0cmluZ1xuICAgKiBAcmV0dXJucyBUaGUgYnVpbHQgc3RyaW5nXG4gICAqL1xuICB0b1N0cmluZygpOiBzdHJpbmcge1xuICAgIHJldHVybiB0aGlzLnBhcnRzLmpvaW4oXCJcIik7XG4gIH1cblxuICAvKipcbiAgICogQ2hlY2tzIGlmIGEgc3RyaW5nIGNvbnRhaW5zIG9yIGVuZHMgd2l0aCBsaW5lIGJyZWFrc1xuICAgKiBAcGFyYW0gc3RyIFRoZSBzdHJpbmcgdG8gY2hlY2tcbiAgICogQHJldHVybnMgVHJ1ZSBpZiB0aGUgc3RyaW5nIGNvbnRhaW5zIGFueSBsaW5lIGJyZWFrc1xuICAgKi9cbiAgc3RhdGljIGlzTXVsdGlsaW5lKHN0cjogc3RyaW5nKTogYm9vbGVhbiB7XG4gICAgLy8gQ291bnQgYWxsIGxpbmUgYnJlYWtzIGluIHRoZSBzdHJpbmdcbiAgICBsZXQgbGluZUJyZWFrQ291bnQgPSAwO1xuXG4gICAgZm9yIChsZXQgaSA9IDA7IGkgPCBzdHIubGVuZ3RoOyBpKyspIHtcbiAgICAgIC8vIENoZWNrIGZvciBcXG4gKExpbmUgRmVlZClcbiAgICAgIGlmIChzdHJbaV0gPT09IFwiXFxuXCIpIHtcbiAgICAgICAgbGluZUJyZWFrQ291bnQrKztcbiAgICAgIH1cbiAgICAgIC8vIENoZWNrIGZvciBcXHIgKENhcnJpYWdlIFJldHVybikgbm90IGZvbGxvd2VkIGJ5IFxcbiAodG8gYXZvaWQgZG91YmxlIGNvdW50aW5nIFxcclxcbilcbiAgICAgIGVsc2UgaWYgKFxuICAgICAgICBzdHJbaV0gPT09IFwiXFxyXCIgJiZcbiAgICAgICAgKGkgPT09IHN0ci5sZW5ndGggLSAxIHx8IHN0cltpICsgMV0gIT09IFwiXFxuXCIpXG4gICAgICApIHtcbiAgICAgICAgbGluZUJyZWFrQ291bnQrKztcbiAgICAgIH1cbiAgICB9XG5cbiAgICByZXR1cm4gbGluZUJyZWFrQ291bnQgPiAwO1xuICB9XG59XG5cbi8qKlxuICogQSBsaWdodHdlaWdodCBieXRlIGFjY3VtdWxhdG9yIGZvciByYXcgc3Rkb3V0L3N0ZGVyciBjYXB0dXJlLlxuICpcbiAqIENodW5rcyBhcmUgc3RvcmVkIGFzLWlzIGFuZCBvbmx5IGpvaW5lZCAoYW5kLCBmb3IgdGV4dCBjb25zdW1lcnMsXG4gKiBkZWNvZGVkKSBvbmNlIGF0IHRoZSBlbmQuIERlY29kaW5nIHRoZSBDT05DQVRFTkFURUQgYnVmZmVyIGluIG9uZVxuICogcGFzcyDigJQgcmF0aGVyIHRoYW4gcGVyLWNodW5rIOKAlCBpcyB3aGF0IGtlZXBzIG11bHRpLWJ5dGUgVVRGLThcbiAqIHNlcXVlbmNlcyB0aGF0IHN0cmFkZGxlIGEgY2h1bmsgYm91bmRhcnkgaW50YWN0LCBhbmQgd2hhdCBrZWVwc1xuICogYmluYXJ5IHBheWxvYWRzIChleGlmdG9vbCAtYikgYnl0ZS1leGFjdC5cbiAqL1xuZXhwb3J0IGNsYXNzIEJ5dGVCdWlsZGVyIHtcbiAgcHJpdmF0ZSBjaHVua3M6IFVpbnQ4QXJyYXlbXSA9IFtdO1xuICBwcml2YXRlIGRlY29kZXIgPSBuZXcgVGV4dERlY29kZXIoXCJ1dGYtOFwiKTtcbiAgcHJpdmF0ZSBlbmNvZGVyID0gbmV3IFRleHRFbmNvZGVyKCk7XG5cbiAgLyoqXG4gICAqIEFwcGVuZHMgYSBjaHVuay4gU3RyaW5ncyBhcmUgZW5jb2RlZCB0byBVVEYtOCBieXRlcyBzbyBtaXhlZFxuICAgKiBwcm9kdWNlcnMgc3RpbGwgYWNjdW11bGF0ZSBpbnRvIG9uZSBjb2hlcmVudCBieXRlIHN0cmVhbS5cbiAgICogQHJldHVybnMgVGhlIEJ5dGVCdWlsZGVyIGluc3RhbmNlIGZvciBjaGFpbmluZ1xuICAgKi9cbiAgYXBwZW5kKGNodW5rOiBVaW50OEFycmF5IHwgc3RyaW5nKTogQnl0ZUJ1aWxkZXIge1xuICAgIHRoaXMuY2h1bmtzLnB1c2goXG4gICAgICB0eXBlb2YgY2h1bmsgPT09IFwic3RyaW5nXCIgPyB0aGlzLmVuY29kZXIuZW5jb2RlKGNodW5rKSA6IGNodW5rLFxuICAgICk7XG4gICAgcmV0dXJuIHRoaXM7XG4gIH1cblxuICAvKipcbiAgICogQ2xlYXJzIGFsbCBhY2N1bXVsYXRlZCBieXRlc1xuICAgKiBAcmV0dXJucyBUaGUgQnl0ZUJ1aWxkZXIgaW5zdGFuY2UgZm9yIGNoYWluaW5nXG4gICAqL1xuICBjbGVhcigpOiBCeXRlQnVpbGRlciB7XG4gICAgdGhpcy5jaHVua3MgPSBbXTtcbiAgICByZXR1cm4gdGhpcztcbiAgfVxuXG4gIC8qKlxuICAgKiBUb3RhbCBhY2N1bXVsYXRlZCBzaXplIGluIGJ5dGVzXG4gICAqL1xuICBnZXQgYnl0ZUxlbmd0aCgpOiBudW1iZXIge1xuICAgIHJldHVybiB0aGlzLmNodW5rcy5yZWR1Y2UoKGFjYywgYykgPT4gYWNjICsgYy5ieXRlTGVuZ3RoLCAwKTtcbiAgfVxuXG4gIC8qKlxuICAgKiBSZXR1cm5zIHRoZSBhY2N1bXVsYXRlZCBieXRlcyBhcyBhIHNpbmdsZSBVaW50OEFycmF5XG4gICAqL1xuICB0b0J5dGVzKCk6IFVpbnQ4QXJyYXkge1xuICAgIGNvbnN0IG91dCA9IG5ldyBVaW50OEFycmF5KHRoaXMuYnl0ZUxlbmd0aCk7XG4gICAgbGV0IG9mZnNldCA9IDA7XG4gICAgZm9yIChjb25zdCBjIG9mIHRoaXMuY2h1bmtzKSB7XG4gICAgICBvdXQuc2V0KGMsIG9mZnNldCk7XG4gICAgICBvZmZzZXQgKz0gYy5ieXRlTGVuZ3RoO1xuICAgIH1cbiAgICByZXR1cm4gb3V0O1xuICB9XG5cbiAgLyoqXG4gICAqIERlY29kZXMgdGhlIGFjY3VtdWxhdGVkIGJ5dGVzIGFzIFVURi04IHRleHQgKHNpbmdsZS1wYXNzIGRlY29kZSlcbiAgICovXG4gIHRvU3RyaW5nKCk6IHN0cmluZyB7XG4gICAgcmV0dXJuIHRoaXMuZGVjb2Rlci5kZWNvZGUodGhpcy50b0J5dGVzKCkpO1xuICB9XG59XG4iLAogICAgImltcG9ydCB7IE1lbW9yeUZpbGVTeXN0ZW0sIFplcm9QZXJsIH0gZnJvbSBcIi4vemVyb3BlcmxcIjtcbmltcG9ydCBleGlmdG9vbCBmcm9tIFwiLi9leGlmdG9vbFwiIHdpdGggeyB0eXBlOiBcInRleHRcIiB9O1xuaW1wb3J0IHsgQnl0ZUJ1aWxkZXIgfSBmcm9tIFwiLi9zYlwiO1xuXG50eXBlIEZldGNoTGlrZSA9ICguLi5hcmdzOiB1bmtub3duW10pID0+IFByb21pc2U8UmVzcG9uc2U+O1xuXG5leHBvcnQgdHlwZSBFeGlmVGFncyA9IFJlY29yZDxcblx0c3RyaW5nLFxuXHRzdHJpbmcgfCBudW1iZXIgfCBib29sZWFuIHwgKHN0cmluZyB8IG51bWJlciB8IGJvb2xlYW4pW11cbj47XG5cbi8qKlxuICogQ29uZmlndXJhdGlvbiBvcHRpb25zIGZvciBwYXJzaW5nIGZpbGUgbWV0YWRhdGEgd2l0aCBFeGlmVG9vbFxuICogQHRlbXBsYXRlIFRyYW5zZm9ybVJldHVybiBUaGUgdHlwZSBvZiB0aGUgdHJhbnNmb3JtZWQgb3V0cHV0IGRhdGFcbiAqL1xuZXhwb3J0IGludGVyZmFjZSBFeGlmVG9vbE9wdGlvbnM8VHJhbnNmb3JtUmV0dXJuID0gdW5rbm93bj4ge1xuXHQvKipcblx0ICogQWRkaXRpb25hbCBjb21tYW5kLWxpbmUgYXJndW1lbnRzIHRvIHBhc3MgdG8gRXhpZlRvb2xcblx0ICpcblx0ICogQGV4YW1wbGVcblx0ICogLy8gRXh0cmFjdCBzcGVjaWZpYyB0YWdzXG5cdCAqIGFyZ3M6IFtcIi1BdXRob3JcIiwgXCItQ3JlYXRlRGF0ZVwiXVxuXHQgKlxuXHQgKiBAZXhhbXBsZVxuXHQgKiAvLyBPdXRwdXQgYXMgSlNPTlxuXHQgKiBhcmdzOiBbXCItanNvblwiLCBcIi1uXCJdXG5cdCAqXG5cdCAqIEBzZWUgaHR0cHM6Ly9leGlmdG9vbC5vcmcvZXhpZnRvb2xfcG9kLmh0bWwgZm9yIGFsbCBhdmFpbGFibGUgb3B0aW9uc1xuXHQgKi9cblx0YXJncz86IHN0cmluZ1tdO1xuXG5cdC8qKlxuXHQgKiBDdXN0b20gZmV0Y2ggaW1wbGVtZW50YXRpb24gZm9yIGxvYWRpbmcgdGhlIFdBU00gbW9kdWxlXG5cdCAqXG5cdCAqIE9ubHkgbmVlZGVkIGZvciBlbnZpcm9ubWVudHMgd2l0aCBjdXN0b20gZmV0Y2ggcG9seWZpbGxzXG5cdCAqL1xuXHRmZXRjaD86IEZldGNoTGlrZTtcblxuXHQvKipcblx0ICogVHJhbnNmb3JtIHRoZSByYXcgRXhpZlRvb2wgb3V0cHV0IGludG8gYSBkaWZmZXJlbnQgZm9ybWF0XG5cdCAqXG5cdCAqIEBleGFtcGxlXG5cdCAqIC8vIFBhcnNlIG91dHB1dCBhcyBKU09OXG5cdCAqIHRyYW5zZm9ybTogKGRhdGEpID0+IEpTT04ucGFyc2UoZGF0YSlcblx0ICovXG5cdHRyYW5zZm9ybT86IChkYXRhOiBzdHJpbmcpID0+IFRyYW5zZm9ybVJldHVybjtcblxuXHQvKipcblx0ICogVGhlIEV4aWZUb29sX2NvbmZpZ1xuXHQgKi9cblx0Y29uZmlnPzogQmluYXJ5ZmlsZSB8IEZpbGU7XG5cblx0LyoqXG5cdCAqIFJldHVybiBzdGRvdXQgYXMgcmF3IGJ5dGVzIChVaW50OEFycmF5KSBpbnN0ZWFkIG9mIGRlY29kZWQgdGV4dC5cblx0ICpcblx0ICogUmVxdWlyZWQgZm9yIHVuY29ycnVwdGVkIGJpbmFyeSBleHRyYWN0aW9uIOKAlCBFeGlmVG9vbCdzIGAtYmAgb3V0cHV0XG5cdCAqIChlbWJlZGRlZCBwcmV2aWV3cywgTVBGIGdhaW4gbWFwcywgZGVwdGggbWFwcywgdHJhaWxlciBwYXlsb2Fkcylcblx0ICogaXMgbm90IFVURi04IGFuZCBtdXN0IG5ldmVyIHBhc3MgdGhyb3VnaCBhIHRleHQgZGVjb2RlLlxuXHQgKiBgdHJhbnNmb3JtYCBpcyBub3QgYXBwbGllZCBpbiBiaW5hcnkgbW9kZS5cblx0ICpcblx0ICogQGV4YW1wbGVcblx0ICogLy8gRXh0cmFjdCBhbiBlbWJlZGRlZCB0aHVtYm5haWwsIGJ5dGUtZXhhY3Rcblx0ICogY29uc3QgcmVzdWx0ID0gYXdhaXQgcGFyc2VNZXRhZGF0YShmaWxlLCB7XG5cdCAqICAgYXJnczogW1wiLWJcIiwgXCItVGh1bWJuYWlsSW1hZ2VcIiwgXCItbVwiLCBcIi1xXCJdLFxuXHQgKiAgIGJpbmFyeTogdHJ1ZSxcblx0ICogfSk7XG5cdCAqIGlmIChyZXN1bHQuc3VjY2Vzcykge1xuXHQgKiAgIGNvbnN0IGJsb2IgPSBuZXcgQmxvYihbcmVzdWx0LmRhdGFdLCB7IHR5cGU6IFwiaW1hZ2UvanBlZ1wiIH0pO1xuXHQgKiB9XG5cdCAqL1xuXHRiaW5hcnk/OiBib29sZWFuO1xufVxuXG4vKipcbiAqIFJlcHJlc2VudHMgYSBiaW5hcnkgZmlsZSBmb3IgbWV0YWRhdGEgZXh0cmFjdGlvblxuICovXG50eXBlIEJpbmFyeWZpbGUgPSB7XG5cdC8qKiBGaWxlbmFtZSB3aXRoIGV4dGVuc2lvbiAoZS5nLiwgXCJpbWFnZS5qcGdcIikgKi9cblx0bmFtZTogc3RyaW5nO1xuXHQvKiogVGhlIGJpbmFyeSBjb250ZW50IG9mIHRoZSBmaWxlICovXG5cdGRhdGE6IFVpbnQ4QXJyYXkgfCBCbG9iO1xufTtcblxuLyoqXG4gKiBSZXN1bHQgb2YgYW4gRXhpZlRvb2wgbWV0YWRhdGEgZXh0cmFjdGlvbiBvcGVyYXRpb25cbiAqIEB0ZW1wbGF0ZSBUT3V0cHV0IFRoZSB0eXBlIG9mIHRoZSBvdXRwdXQgZGF0YSBhZnRlciB0cmFuc2Zvcm1hdGlvblxuICovXG50eXBlIEV4aWZUb29sT3V0cHV0PFRPdXRwdXQ+ID1cblx0fCB7XG5cdFx0XHQvKiogVHJ1ZSB3aGVuIG1ldGFkYXRhIHdhcyBzdWNjZXNzZnVsbHkgZXh0cmFjdGVkICovXG5cdFx0XHRzdWNjZXNzOiB0cnVlO1xuXHRcdFx0LyoqIFRoZSBleHRyYWN0ZWQgbWV0YWRhdGEsIHRyYW5zZm9ybWVkIGlmIGEgdHJhbnNmb3JtIGZ1bmN0aW9uIHdhcyBwcm92aWRlZCAqL1xuXHRcdFx0ZGF0YTogVE91dHB1dDtcblx0XHRcdC8qKiBBbHdheXMgMCBmb3Igc3VjY2VzcyAqL1xuXHRcdFx0ZXhpdENvZGU6IDA7XG5cdCAgfVxuXHR8IHtcblx0XHRcdC8qKiBGYWxzZSB3aGVuIG1ldGFkYXRhIGV4dHJhY3Rpb24gZmFpbGVkICovXG5cdFx0XHRzdWNjZXNzOiBmYWxzZTtcblx0XHRcdC8qKiBObyBkYXRhIGF2YWlsYWJsZSBvbiBmYWlsdXJlICovXG5cdFx0XHRkYXRhOiB1bmRlZmluZWQ7XG5cdFx0XHQvKiogRXJyb3IgbWVzc2FnZSBleHBsYWluaW5nIHdoeSB0aGUgb3BlcmF0aW9uIGZhaWxlZCAqL1xuXHRcdFx0ZXJyb3I6IHN0cmluZztcblx0XHRcdC8qKiBOb24temVybyBleGl0IGNvZGUgaW5kaWNhdGluZyB0aGUgdHlwZSBvZiBmYWlsdXJlICovXG5cdFx0XHRleGl0Q29kZTogbnVtYmVyIHwgdW5kZWZpbmVkO1xuXHQgIH07XG5cbi8qKlxuICogQ2FjaGVkIFplcm9QZXJsIGluc3RhbmNlIGFuZCBmaWxlc3lzdGVtIHVzaW5nIFdlYWtSZWZcbiAqL1xubGV0IGNhY2hlZFBlcmxSZWY6IFdlYWtSZWY8WmVyb1Blcmw+IHwgbnVsbCA9IG51bGw7XG5sZXQgY2FjaGVkRmlsZVN5c3RlbVJlZjogV2Vha1JlZjxNZW1vcnlGaWxlU3lzdGVtPiB8IG51bGwgPSBudWxsO1xuXG4vKipcbiAqIEdsb2JhbCBvdXRwdXQgYnVmZmVycy5cbiAqXG4gKiBSYXcgYnl0ZXMgYXJlIGFjY3VtdWxhdGVkIHBlciBjaHVuayBhbmQgb25seSBkZWNvZGVkIChmb3IgdGV4dFxuICogY29uc3VtZXJzKSBvciBjb25jYXRlbmF0ZWQgKGZvciBiaW5hcnkgY29uc3VtZXJzKSBvbmNlLCBhdCB0aGUgZW5kXG4gKiBvZiBhIHJ1bi4gUGVyLWNodW5rIGRlY29kaW5nIOKAlCB0aGUgcHJldmlvdXMgYmVoYXZpb3Ig4oCUIGNvcnJ1cHRlZFxuICogYmluYXJ5IHN0ZG91dCAoZXhpZnRvb2wgLWIpIGlycmV2ZXJzaWJseSBhbmQgY291bGQgc3BsaXQgbXVsdGktYnl0ZVxuICogVVRGLTggc2VxdWVuY2VzIGFjcm9zcyBjaHVuayBib3VuZGFyaWVzLlxuICovXG5jb25zdCBzdGRvdXQgPSBuZXcgQnl0ZUJ1aWxkZXIoKTtcbmNvbnN0IHN0ZGVyciA9IG5ldyBCeXRlQnVpbGRlcigpO1xuXG4vKipcbiAqIEdldCBvciBjcmVhdGUgdGhlIHNoYXJlZCBaZXJvUGVybCBpbnN0YW5jZVxuICovXG5hc3luYyBmdW5jdGlvbiBnZXRaZXJvUGVybChcblx0ZmV0Y2hGbj86IEZldGNoTGlrZSxcbik6IFByb21pc2U8eyBwZXJsOiBaZXJvUGVybDsgZmlsZVN5c3RlbTogTWVtb3J5RmlsZVN5c3RlbSB9PiB7XG5cdGxldCBjYWNoZWRQZXJsID0gY2FjaGVkUGVybFJlZj8uZGVyZWYoKTtcblx0bGV0IGNhY2hlZEZpbGVTeXN0ZW0gPSBjYWNoZWRGaWxlU3lzdGVtUmVmPy5kZXJlZigpO1xuXG5cdGlmIChjYWNoZWRQZXJsICYmIGNhY2hlZEZpbGVTeXN0ZW0pIHtcblx0XHRyZXR1cm4geyBwZXJsOiBjYWNoZWRQZXJsLCBmaWxlU3lzdGVtOiBjYWNoZWRGaWxlU3lzdGVtIH07XG5cdH1cblxuXHRjYWNoZWRGaWxlU3lzdGVtID0gbmV3IE1lbW9yeUZpbGVTeXN0ZW0oeyBcIi9cIjogXCJcIiB9KTtcblx0Y2FjaGVkRmlsZVN5c3RlbS5hZGRGaWxlKFwiL2V4aWZ0b29sXCIsIGV4aWZ0b29sKTtcblxuXHRjYWNoZWRQZXJsID0gYXdhaXQgWmVyb1BlcmwuY3JlYXRlKHtcblx0XHRmaWxlU3lzdGVtOiBjYWNoZWRGaWxlU3lzdGVtLFxuXHRcdC8vIFJhdyBjaHVua3Mg4oCUIEJ5dGVCdWlsZGVyIGhhbmRsZXMgdGhlIChzaW5nbGUsIGZpbmFsKSBkZWNvZGUuXG5cdFx0b3V0cHV0QnVmZmVyczogdHJ1ZSxcblx0XHRzdGRvdXQ6IChkYXRhKSA9PiB7XG5cdFx0XHRzdGRvdXQuYXBwZW5kKGRhdGEpO1xuXHRcdH0sXG5cdFx0c3RkZXJyOiAoZGF0YSkgPT4ge1xuXHRcdFx0c3RkZXJyLmFwcGVuZChkYXRhKTtcblx0XHR9LFxuXHRcdGZldGNoOiBmZXRjaEZuLFxuXHR9KTtcblxuXHRjYWNoZWRQZXJsUmVmID0gbmV3IFdlYWtSZWYoY2FjaGVkUGVybCk7XG5cdGNhY2hlZEZpbGVTeXN0ZW1SZWYgPSBuZXcgV2Vha1JlZihjYWNoZWRGaWxlU3lzdGVtKTtcblxuXHRyZXR1cm4geyBwZXJsOiBjYWNoZWRQZXJsLCBmaWxlU3lzdGVtOiBjYWNoZWRGaWxlU3lzdGVtIH07XG59XG5cbi8qKlxuICogQ2xlYW4gdXAgdGVtcG9yYXJ5IGZpbGVzIGZyb20gdGhlIGZpbGVzeXN0ZW1cbiAqL1xuZnVuY3Rpb24gY2xlYW51cFRlbXBGaWxlcyhmaWxlU3lzdGVtOiBNZW1vcnlGaWxlU3lzdGVtLCBwYXRoczogc3RyaW5nW10pOiB2b2lkIHtcblx0Zm9yIChjb25zdCBwYXRoIG9mIHBhdGhzKSB7XG5cdFx0dHJ5IHtcblx0XHRcdGZpbGVTeXN0ZW0ucmVtb3ZlRmlsZShwYXRoKTtcblx0XHR9IGNhdGNoIHtcblx0XHRcdC8vIElnbm9yZSBlcnJvcnMgaWYgZmlsZSBkb2Vzbid0IGV4aXN0XG5cdFx0fVxuXHR9XG59XG5cbi8qKlxuICogVHJhbnNmb3JtIHRhZ3Mgb2JqZWN0IGludG8gRXhpZlRvb2wgY29tbWFuZC1saW5lIGFyZ3VtZW50c1xuICovXG5mdW5jdGlvbiB0cmFuc2Zvcm1UYWdzKHRhZ3M6IEV4aWZUYWdzKTogc3RyaW5nW10ge1xuXHRyZXR1cm4gT2JqZWN0LmVudHJpZXModGFncykuZmxhdE1hcCgoW25hbWUsIHZhbHVlXSkgPT5cblx0XHRBcnJheS5pc0FycmF5KHZhbHVlKVxuXHRcdFx0PyB2YWx1ZS5tYXAoKHZhbHVlKSA9PiBgLSR7bmFtZX09JHt2YWx1ZX1gKVxuXHRcdFx0OiBbYC0ke25hbWV9PSR7dmFsdWV9YF0sXG5cdCk7XG59XG5cbi8qKlxuICogRXh0cmFjdCBtZXRhZGF0YSBmcm9tIGEgZmlsZSB1c2luZyBFeGlmVG9vbFxuICpcbiAqIEB0ZW1wbGF0ZSBUUmV0dXJuIFR5cGUgb2YgdGhlIHJldHVybmVkIGRhdGEgYWZ0ZXIgdHJhbnNmb3JtYXRpb24gKGRlZmF1bHRzIHRvIHN0cmluZylcbiAqIEBwYXJhbSBmaWxlIEZpbGUgdG8gZXh0cmFjdCBtZXRhZGF0YSBmcm9tXG4gKiBAcGFyYW0gb3B0aW9ucyBDb25maWd1cmF0aW9uIG9wdGlvbnNcbiAqIEByZXR1cm5zIFByb21pc2UgcmVzb2x2aW5nIHRvIHRoZSBleHRyYWN0aW9uIHJlc3VsdFxuICpcbiAqIEBleGFtcGxlXG4gKiAvLyBCYXNpYyB1c2FnZSB3aXRoIGJyb3dzZXIgRmlsZSBvYmplY3RcbiAqIGNvbnN0IGlucHV0ID0gZG9jdW1lbnQucXVlcnlTZWxlY3RvcignaW5wdXRbdHlwZT1cImZpbGVcIl0nKTtcbiAqIGlucHV0LmFkZEV2ZW50TGlzdGVuZXIoJ2NoYW5nZScsIGFzeW5jICgpID0+IHtcbiAqICAgY29uc3QgZmlsZSA9IGlucHV0LmZpbGVzWzBdO1xuICogICBjb25zdCByZXN1bHQgPSBhd2FpdCBwYXJzZU1ldGFkYXRhKGZpbGUpO1xuICogICBpZiAocmVzdWx0LnN1Y2Nlc3MpIHtcbiAqICAgICBjb25zb2xlLmxvZyhyZXN1bHQuZGF0YSk7IC8vIFJhdyBFeGlmVG9vbCBvdXRwdXQgYXMgc3RyaW5nXG4gKiAgIH1cbiAqIH0pO1xuICpcbiAqIEBleGFtcGxlXG4gKiAvLyBFeHRyYWN0IHNwZWNpZmljIHRhZ3MgYW5kIHRyYW5zZm9ybSB0byBKU09OXG4gKiBjb25zdCByZXN1bHQgPSBhd2FpdCBwYXJzZU1ldGFkYXRhKGZpbGUsIHtcbiAqICAgYXJnczogW1wiLWpzb25cIl0sXG4gKiAgIHRyYW5zZm9ybTogKGRhdGEpID0+IEpTT04ucGFyc2UoZGF0YSlcbiAqIH0pO1xuICogaWYgKHJlc3VsdC5zdWNjZXNzKSB7XG4gKiAgIGNvbnNvbGUubG9nKHJlc3VsdC5kYXRhKTsgLy8gVHlwZWQgYWNjZXNzIHRvIHNwZWNpZmljIG1ldGFkYXRhXG4gKiB9XG4gKlxuICogQGV4YW1wbGVcbiAqIC8vIEV4dHJhY3QgYW4gZW1iZWRkZWQgYmluYXJ5LCBieXRlLWV4YWN0IChzZWUgYGJpbmFyeWAgb3B0aW9uKVxuICogY29uc3QgcmVzdWx0ID0gYXdhaXQgcGFyc2VNZXRhZGF0YShmaWxlLCB7XG4gKiAgIGFyZ3M6IFtcIi1iXCIsIFwiLVRodW1ibmFpbEltYWdlXCIsIFwiLW1cIiwgXCItcVwiXSxcbiAqICAgYmluYXJ5OiB0cnVlLFxuICogfSk7XG4gKiBpZiAocmVzdWx0LnN1Y2Nlc3MpIHtcbiAqICAgY29uc29sZS5sb2cocmVzdWx0LmRhdGEuYnl0ZUxlbmd0aCk7IC8vIFVpbnQ4QXJyYXlcbiAqIH1cbiAqL1xuZXhwb3J0IGFzeW5jIGZ1bmN0aW9uIHBhcnNlTWV0YWRhdGEoXG5cdGZpbGU6IEJpbmFyeWZpbGUgfCBGaWxlLFxuXHRvcHRpb25zOiBPbWl0PEV4aWZUb29sT3B0aW9uczxuZXZlcj4sIFwidHJhbnNmb3JtXCIgfCBcImJpbmFyeVwiPiAmIHtcblx0XHRiaW5hcnk6IHRydWU7XG5cdH0sXG4pOiBQcm9taXNlPEV4aWZUb29sT3V0cHV0PFVpbnQ4QXJyYXk+PjtcbmV4cG9ydCBhc3luYyBmdW5jdGlvbiBwYXJzZU1ldGFkYXRhPFRSZXR1cm4gPSBzdHJpbmc+KFxuXHRmaWxlOiBCaW5hcnlmaWxlIHwgRmlsZSxcblx0b3B0aW9ucz86IEV4aWZUb29sT3B0aW9uczxUUmV0dXJuPiAmIHsgYmluYXJ5PzogZmFsc2UgfSxcbik6IFByb21pc2U8RXhpZlRvb2xPdXRwdXQ8VFJldHVybj4+O1xuZXhwb3J0IGFzeW5jIGZ1bmN0aW9uIHBhcnNlTWV0YWRhdGE8VFJldHVybiA9IHN0cmluZz4oXG5cdGZpbGU6IEJpbmFyeWZpbGUgfCBGaWxlLFxuXHRvcHRpb25zOiBFeGlmVG9vbE9wdGlvbnM8VFJldHVybj4gPSB7fSxcbik6IFByb21pc2U8RXhpZlRvb2xPdXRwdXQ8VFJldHVybiB8IFVpbnQ4QXJyYXk+PiB7XG5cdGNvbnN0IHsgcGVybCwgZmlsZVN5c3RlbSB9ID0gYXdhaXQgZ2V0WmVyb1Blcmwob3B0aW9ucy5mZXRjaCk7XG5cdGNvbnN0IHRlbXBGaWxlczogc3RyaW5nW10gPSBbXTtcblxuXHRzdGRvdXQuY2xlYXIoKTtcblx0c3RkZXJyLmNsZWFyKCk7XG5cdGF3YWl0IHBlcmwucmVzZXQoKTtcblxuXHR0cnkge1xuXHRcdGNvbnN0IGlucHV0UGF0aCA9IGAvJHtmaWxlLm5hbWV9YDtcblx0XHRpZiAoZmlsZSBpbnN0YW5jZW9mIEZpbGUpIHtcblx0XHRcdGZpbGVTeXN0ZW0uYWRkRmlsZShpbnB1dFBhdGgsIGZpbGUpO1xuXHRcdH0gZWxzZSB7XG5cdFx0XHRmaWxlU3lzdGVtLmFkZEZpbGUoaW5wdXRQYXRoLCBmaWxlLmRhdGEpO1xuXHRcdH1cblx0XHR0ZW1wRmlsZXMucHVzaChpbnB1dFBhdGgpO1xuXG5cdFx0Ly8gRXhpZlRvb2wgaG9ub3JzIC1jb25maWcgT05MWSBhcyB0aGUgRklSU1QgYXJndW1lbnQg4oCUIGFwcGVuZGVkXG5cdFx0Ly8gYW55d2hlcmUgZWxzZSBpdCBpcyBzaWxlbnRseSBpZ25vcmVkIGFuZCB1c2VyLWRlZmluZWQgdGFnc1xuXHRcdC8vIG5ldmVyIGxvYWQuIEJ1aWxkIGl0IGJlZm9yZSB0aGUgY2FsbGVyJ3MgYXJncy5cblx0XHRjb25zdCBhcmdzID0gW107XG5cdFx0aWYgKG9wdGlvbnMuY29uZmlnKSB7XG5cdFx0XHRjb25zdCBjb25maWdQYXRoID0gYC8ke29wdGlvbnMuY29uZmlnLm5hbWV9YDtcblx0XHRcdGlmIChvcHRpb25zLmNvbmZpZyBpbnN0YW5jZW9mIEZpbGUpIHtcblx0XHRcdFx0ZmlsZVN5c3RlbS5hZGRGaWxlKGNvbmZpZ1BhdGgsIG9wdGlvbnMuY29uZmlnKTtcblx0XHRcdH0gZWxzZSB7XG5cdFx0XHRcdGZpbGVTeXN0ZW0uYWRkRmlsZShjb25maWdQYXRoLCBvcHRpb25zLmNvbmZpZy5kYXRhKTtcblx0XHRcdH1cblx0XHRcdHRlbXBGaWxlcy5wdXNoKGNvbmZpZ1BhdGgpO1xuXHRcdFx0YXJncy5wdXNoKGAtY29uZmlnYCwgY29uZmlnUGF0aCk7XG5cdFx0fVxuXHRcdGFyZ3MucHVzaCguLi4ob3B0aW9ucy5hcmdzIHx8IFtdKSk7XG5cblx0XHRhcmdzLnB1c2goaW5wdXRQYXRoKTtcblxuXHRcdGNvbnN0IHJlc3VsdCA9IGF3YWl0IHBlcmwucnVuRmlsZShcIi9leGlmdG9vbFwiLCBhcmdzKTtcblx0XHRwZXJsLmZsdXNoKCk7XG5cblx0XHRjb25zdCBzdGRlcnJDb250ZW50ID0gc3RkZXJyLnRvU3RyaW5nKCk7XG5cblx0XHRpZiAoIXJlc3VsdC5zdWNjZXNzIHx8IHJlc3VsdC5leGl0Q29kZSAhPT0gMCkge1xuXHRcdFx0Y29uc3QgcGVybEVycm9yID0gcGVybC5nZXRMYXN0RXJyb3IoKTtcblxuXHRcdFx0cmV0dXJuIHtcblx0XHRcdFx0c3VjY2VzczogZmFsc2UsXG5cdFx0XHRcdGRhdGE6IHVuZGVmaW5lZCxcblx0XHRcdFx0ZXJyb3I6IHBlcmxFcnJvciB8fCBzdGRlcnJDb250ZW50IHx8IFwiVW5rbm93biBlcnJvclwiLFxuXHRcdFx0XHRleGl0Q29kZTogcmVzdWx0LmV4aXRDb2RlLFxuXHRcdFx0fTtcblx0XHR9XG5cblx0XHRpZiAoc3RkZXJyQ29udGVudCAmJiBzdGRlcnJDb250ZW50LnRyaW0oKSkge1xuXHRcdFx0cmV0dXJuIHtcblx0XHRcdFx0c3VjY2VzczogZmFsc2UsXG5cdFx0XHRcdGRhdGE6IHVuZGVmaW5lZCxcblx0XHRcdFx0ZXJyb3I6IHN0ZGVyckNvbnRlbnQsXG5cdFx0XHRcdGV4aXRDb2RlOiAwLFxuXHRcdFx0fTtcblx0XHR9XG5cblx0XHQvLyDilIDilIAgQmluYXJ5IG1vZGU6IHJhdyBzdGRvdXQgYnl0ZXMsIG5vIGRlY29kZSwgbm8gdHJhbnNmb3JtIOKUgOKUgFxuXHRcdGlmIChvcHRpb25zLmJpbmFyeSkge1xuXHRcdFx0aWYgKHN0ZG91dC5ieXRlTGVuZ3RoID09PSAwKSB7XG5cdFx0XHRcdHJldHVybiB7XG5cdFx0XHRcdFx0c3VjY2VzczogZmFsc2UsXG5cdFx0XHRcdFx0ZGF0YTogdW5kZWZpbmVkLFxuXHRcdFx0XHRcdGVycm9yOiBcIk5vIG91dHB1dCBkYXRhIGZyb20gRXhpZlRvb2xcIixcblx0XHRcdFx0XHRleGl0Q29kZTogMCxcblx0XHRcdFx0fTtcblx0XHRcdH1cblx0XHRcdHJldHVybiB7XG5cdFx0XHRcdHN1Y2Nlc3M6IHRydWUsXG5cdFx0XHRcdGRhdGE6IHN0ZG91dC50b0J5dGVzKCksXG5cdFx0XHRcdGV4aXRDb2RlOiAwLFxuXHRcdFx0fTtcblx0XHR9XG5cblx0XHRjb25zdCBzdGRvdXRDb250ZW50ID0gc3Rkb3V0LnRvU3RyaW5nKCk7XG5cblx0XHRpZiAoIXN0ZG91dENvbnRlbnQgfHwgIXN0ZG91dENvbnRlbnQudHJpbSgpKSB7XG5cdFx0XHRyZXR1cm4ge1xuXHRcdFx0XHRzdWNjZXNzOiBmYWxzZSxcblx0XHRcdFx0ZGF0YTogdW5kZWZpbmVkLFxuXHRcdFx0XHRlcnJvcjogXCJObyBvdXRwdXQgZGF0YSBmcm9tIEV4aWZUb29sXCIsXG5cdFx0XHRcdGV4aXRDb2RlOiAwLFxuXHRcdFx0fTtcblx0XHR9XG5cblx0XHRsZXQgZGF0YTogVFJldHVybjtcblx0XHRpZiAob3B0aW9ucy50cmFuc2Zvcm0pIHtcblx0XHRcdGRhdGEgPSBvcHRpb25zLnRyYW5zZm9ybShzdGRvdXRDb250ZW50KTtcblx0XHR9IGVsc2Uge1xuXHRcdFx0ZGF0YSA9IHN0ZG91dENvbnRlbnQgYXMgdW5rbm93biBhcyBUUmV0dXJuO1xuXHRcdH1cblxuXHRcdHJldHVybiB7XG5cdFx0XHRzdWNjZXNzOiB0cnVlLFxuXHRcdFx0ZGF0YTogZGF0YSxcblx0XHRcdGV4aXRDb2RlOiAwLFxuXHRcdH07XG5cdH0gZmluYWxseSB7XG5cdFx0Y2xlYW51cFRlbXBGaWxlcyhmaWxlU3lzdGVtLCB0ZW1wRmlsZXMpO1xuXHR9XG59XG5cbi8qKlxuICogV3JpdGUgbWV0YWRhdGEgdG8gYSBmaWxlIHVzaW5nIEV4aWZUb29sXG4gKlxuICogVGhpcyBmdW5jdGlvbiBtb2RpZmllcyBhbiBleGlzdGluZyBmaWxlIGJ5IHdyaXRpbmcgbmV3IG1ldGFkYXRhIHRhZ3Mgb3IgdXBkYXRpbmcgZXhpc3Rpbmcgb25lcy5cbiAqIFRoZSBvcGVyYXRpb24gcnVucyBlbnRpcmVseSBpbiB0aGUgYnJvd3NlciB1c2luZyBXZWJBc3NlbWJseSB3aXRob3V0IHJlcXVpcmluZyBzZXJ2ZXIgdXBsb2Fkcy5cbiAqXG4gKiBAcGFyYW0gZmlsZSBGaWxlIHRvIHdyaXRlIG1ldGFkYXRhIHRvIChCcm93c2VyIEZpbGUgb2JqZWN0IG9yIEJpbmFyeWZpbGUpXG4gKiBAcGFyYW0gdGFncyBPYmplY3QgY29udGFpbmluZyBtZXRhZGF0YSB0YWdzIHRvIHdyaXRlLCB3aGVyZSBrZXlzIGFyZSB0YWcgbmFtZXMgYW5kIHZhbHVlcyBhcmUgdGFnIHZhbHVlc1xuICogQHBhcmFtIG9wdGlvbnMgQ29uZmlndXJhdGlvbiBvcHRpb25zIGZvciB0aGUgd3JpdGUgb3BlcmF0aW9uXG4gKiBAcmV0dXJucyBQcm9taXNlIHJlc29sdmluZyB0byB0aGUgd3JpdGUgb3BlcmF0aW9uIHJlc3VsdCBjb250YWluaW5nIHRoZSBtb2RpZmllZCBmaWxlIGRhdGFcbiAqXG4gKiBAZXhhbXBsZVxuICogLy8gQmFzaWMgdXNhZ2Ugd2l0aCBicm93c2VyIEZpbGUgb2JqZWN0XG4gKiBjb25zdCBpbnB1dCA9IGRvY3VtZW50LnF1ZXJ5U2VsZWN0b3IoJ2lucHV0W3R5cGU9XCJmaWxlXCJdJyk7XG4gKiBpbnB1dC5hZGRFdmVudExpc3RlbmVyKCdjaGFuZ2UnLCBhc3luYyAoKSA9PiB7XG4gKiAgIGNvbnN0IGZpbGUgPSBpbnB1dC5maWxlc1swXTtcbiAqICAgY29uc3QgcmVzdWx0ID0gYXdhaXQgd3JpdGVNZXRhZGF0YShmaWxlLCB7XG4gKiAgICAgJ0F1dGhvcic6ICdKb2huIERvZScsXG4gKiAgICAgJ1RpdGxlJzogJ015IFBob3RvJyxcbiAqICAgICAnS2V5d29yZHMnOiAnbmF0dXJlLHBob3RvZ3JhcGh5J1xuICogICB9KTtcbiAqXG4gKiAgIGlmIChyZXN1bHQuc3VjY2Vzcykge1xuICogICAgIC8vIHJlc3VsdC5kYXRhIGNvbnRhaW5zIHRoZSBtb2RpZmllZCBmaWxlIGFzIEFycmF5QnVmZmVyXG4gKiAgICAgY29uc3QgbW9kaWZpZWRCbG9iID0gbmV3IEJsb2IoW3Jlc3VsdC5kYXRhXSk7XG4gKiAgICAgLy8gU2F2ZSBvciB1c2UgdGhlIG1vZGlmaWVkIGZpbGVcbiAqICAgfVxuICogfSk7XG4gKlxuICogQGV4YW1wbGVcbiAqIC8vIFdyaXRpbmcgbXVsdGlwbGUgdGFnIHR5cGVzXG4gKiBjb25zdCByZXN1bHQgPSBhd2FpdCB3cml0ZU1ldGFkYXRhKGZpbGUsIHtcbiAqICAgJ0F1dGhvcic6ICdKYW5lIFNtaXRoJyxcbiAqICAgJ1JhdGluZyc6IDUsXG4gKiAgICdLZXl3b3Jkcyc6IFsnbGFuZHNjYXBlJywgJ3N1bnNldCcsICdiZWFjaCddLFxuICogICAnR1BTOkdQU0xhdGl0dWRlJzogNDAuNzEyOCxcbiAqICAgJ0dQUzpHUFNMb25naXR1ZGUnOiAtNzQuMDA2MCxcbiAqICAgJ0VYSUY6Q29weXJpZ2h0JzogJ8KpIDIwMjUgSmFuZSBTbWl0aCdcbiAqIH0pO1xuICpcbiAqIEBleGFtcGxlXG4gKiAvLyBVc2luZyB3aXRoIGN1c3RvbSBFeGlmVG9vbCBjb25maWdcbiAqIGNvbnN0IHJlc3VsdCA9IGF3YWl0IHdyaXRlTWV0YWRhdGEoZmlsZSwgdGFncywge1xuICogICBjb25maWc6IGNvbmZpZ0ZpbGUsXG4gKiAgIGFyZ3M6IFsnLW92ZXJ3cml0ZV9vcmlnaW5hbCcsICctUCddXG4gKiB9KTtcbiAqXG4gKiBAZXhhbXBsZVxuICogLy8gSGFuZGxlIGVycm9ycyBwcm9wZXJseVxuICogdHJ5IHtcbiAqICAgY29uc3QgcmVzdWx0ID0gYXdhaXQgd3JpdGVNZXRhZGF0YShmaWxlLCB0YWdzKTtcbiAqICAgaWYgKHJlc3VsdC5zdWNjZXNzKSB7XG4gKiAgICAgY29uc29sZS5sb2coJ01ldGFkYXRhIHdyaXR0ZW4gc3VjY2Vzc2Z1bGx5Jyk7XG4gKiAgICAgZG93bmxvYWRGaWxlKHJlc3VsdC5kYXRhLCBgbW9kaWZpZWRfJHtmaWxlLm5hbWV9YCk7XG4gKiAgIH0gZWxzZSB7XG4gKiAgICAgY29uc29sZS5lcnJvcignV3JpdGUgZmFpbGVkOicsIHJlc3VsdC5lcnJvcik7XG4gKiAgIH1cbiAqIH0gY2F0Y2ggKGVycm9yKSB7XG4gKiAgIGNvbnNvbGUuZXJyb3IoJ09wZXJhdGlvbiBmYWlsZWQ6JywgZXJyb3IpO1xuICogfVxuICpcbiAqIEByZW1hcmtzXG4gKiAtIFRoZSBmdW5jdGlvbiBjcmVhdGVzIGEgdGVtcG9yYXJ5IG91dHB1dCBmaWxlIGludGVybmFsbHkgYW5kIHJldHVybnMgaXRzIGNvbnRlbnRzXG4gKiAtIE9yaWdpbmFsIGZpbGUgaXMgbm90IG1vZGlmaWVkIGluIHBsYWNlOyBhIG5ldyBmaWxlIHdpdGggbWV0YWRhdGEgaXMgZ2VuZXJhdGVkXG4gKiAtIFN1cHBvcnRzIGFsbCBFeGlmVG9vbC1jb21wYXRpYmxlIG1ldGFkYXRhIGZvcm1hdHMgKEVYSUYsIElQVEMsIFhNUCwgZXRjLilcbiAqIC0gVGFnIG5hbWVzIHNob3VsZCBmb2xsb3cgRXhpZlRvb2wgY29udmVudGlvbnMgKGUuZy4sICdFWElGOkFydGlzdCcsICdYTVA6Q3JlYXRvcicpXG4gKiAtIEFycmF5IHZhbHVlcyBpbiB0YWdzIGFyZSBhdXRvbWF0aWNhbGx5IGNvbnZlcnRlZCB0byBtdWx0aXBsZSBFeGlmVG9vbCBhcmd1bWVudHNcbiAqIC0gVGhlIHJldHVybmVkIEFycmF5QnVmZmVyIGNhbiBiZSBjb252ZXJ0ZWQgdG8gYSBCbG9iIGZvciBkb3dubG9hZCBvciBmdXJ0aGVyIHByb2Nlc3NpbmdcbiAqXG4gKiBAc2VlIHtAbGluayBodHRwczovL2V4aWZ0b29sLm9yZy9UYWdOYW1lcy9pbmRleC5odG1sfSBmb3IgY29tcGxldGUgdGFnIHJlZmVyZW5jZVxuICogQHNlZSB7QGxpbmsgcGFyc2VNZXRhZGF0YX0gZm9yIHJlYWRpbmcgbWV0YWRhdGEgZnJvbSBmaWxlc1xuICovXG5leHBvcnQgYXN5bmMgZnVuY3Rpb24gd3JpdGVNZXRhZGF0YShcblx0ZmlsZTogQmluYXJ5ZmlsZSB8IEZpbGUsXG5cdHRhZ3M6IEV4aWZUYWdzLFxuXHRvcHRpb25zOiBFeGlmVG9vbE9wdGlvbnMgPSB7fSxcbik6IFByb21pc2U8RXhpZlRvb2xPdXRwdXQ8QXJyYXlCdWZmZXI+PiB7XG5cdGNvbnN0IHsgcGVybCwgZmlsZVN5c3RlbSB9ID0gYXdhaXQgZ2V0WmVyb1Blcmwob3B0aW9ucy5mZXRjaCk7XG5cdGNvbnN0IHRlbXBGaWxlczogc3RyaW5nW10gPSBbXTtcblxuXHRzdGRvdXQuY2xlYXIoKTtcblx0c3RkZXJyLmNsZWFyKCk7XG5cdGF3YWl0IHBlcmwucmVzZXQoKTtcblxuXHR0cnkge1xuXHRcdGNvbnN0IGlucHV0UGF0aCA9IGAvJHtmaWxlLm5hbWV9YDtcblx0XHRpZiAoZmlsZSBpbnN0YW5jZW9mIEZpbGUpIHtcblx0XHRcdGZpbGVTeXN0ZW0uYWRkRmlsZShpbnB1dFBhdGgsIGZpbGUpO1xuXHRcdH0gZWxzZSB7XG5cdFx0XHRmaWxlU3lzdGVtLmFkZEZpbGUoaW5wdXRQYXRoLCBmaWxlLmRhdGEpO1xuXHRcdH1cblx0XHR0ZW1wRmlsZXMucHVzaChpbnB1dFBhdGgpO1xuXG5cdFx0Ly8gRXhpZlRvb2wgaG9ub3JzIC1jb25maWcgT05MWSBhcyB0aGUgRklSU1QgYXJndW1lbnQg4oCUIGFwcGVuZGVkXG5cdFx0Ly8gYW55d2hlcmUgZWxzZSBpdCBpcyBzaWxlbnRseSBpZ25vcmVkIGFuZCB1c2VyLWRlZmluZWQgdGFnc1xuXHRcdC8vIG5ldmVyIGxvYWQuIEJ1aWxkIGl0IGJlZm9yZSB0aGUgY2FsbGVyJ3MgYXJncy5cblx0XHRjb25zdCBhcmdzID0gW107XG5cdFx0aWYgKG9wdGlvbnMuY29uZmlnKSB7XG5cdFx0XHRjb25zdCBjb25maWdQYXRoID0gYC8ke29wdGlvbnMuY29uZmlnLm5hbWV9YDtcblx0XHRcdGlmIChvcHRpb25zLmNvbmZpZyBpbnN0YW5jZW9mIEZpbGUpIHtcblx0XHRcdFx0ZmlsZVN5c3RlbS5hZGRGaWxlKGNvbmZpZ1BhdGgsIG9wdGlvbnMuY29uZmlnKTtcblx0XHRcdH0gZWxzZSB7XG5cdFx0XHRcdGZpbGVTeXN0ZW0uYWRkRmlsZShjb25maWdQYXRoLCBvcHRpb25zLmNvbmZpZy5kYXRhKTtcblx0XHRcdH1cblx0XHRcdHRlbXBGaWxlcy5wdXNoKGNvbmZpZ1BhdGgpO1xuXHRcdFx0YXJncy5wdXNoKGAtY29uZmlnYCwgY29uZmlnUGF0aCk7XG5cdFx0fVxuXHRcdGFyZ3MucHVzaCguLi4ob3B0aW9ucy5hcmdzIHx8IFtdKSk7XG5cblx0XHRhcmdzLnB1c2goLi4udHJhbnNmb3JtVGFncyh0YWdzKSk7XG5cblx0XHRjb25zdCB0ZW1wRmlsZSA9IGAvJHtjcnlwdG8ucmFuZG9tVVVJRCgpLnJlcGxhY2UoLy0vZywgXCJcIil9LnRtcGA7XG5cdFx0dGVtcEZpbGVzLnB1c2godGVtcEZpbGUpO1xuXG5cdFx0YXJncy5wdXNoKFwiLW9cIiwgdGVtcEZpbGUpO1xuXHRcdGFyZ3MucHVzaChpbnB1dFBhdGgpO1xuXG5cdFx0Y29uc3QgcmVzdWx0ID0gYXdhaXQgcGVybC5ydW5GaWxlKFwiL2V4aWZ0b29sXCIsIGFyZ3MpO1xuXHRcdHBlcmwuZmx1c2goKTtcblxuXHRcdGNvbnN0IHN0ZGVyckNvbnRlbnQgPSBzdGRlcnIudG9TdHJpbmcoKTtcblxuXHRcdGlmICghcmVzdWx0LnN1Y2Nlc3MgfHwgcmVzdWx0LmV4aXRDb2RlICE9PSAwKSB7XG5cdFx0XHRjb25zdCBwZXJsRXJyb3IgPSBwZXJsLmdldExhc3RFcnJvcigpO1xuXG5cdFx0XHRyZXR1cm4ge1xuXHRcdFx0XHRzdWNjZXNzOiBmYWxzZSxcblx0XHRcdFx0ZGF0YTogdW5kZWZpbmVkLFxuXHRcdFx0XHRlcnJvcjogcGVybEVycm9yIHx8IHN0ZGVyckNvbnRlbnQgfHwgXCJVbmtub3duIGVycm9yXCIsXG5cdFx0XHRcdGV4aXRDb2RlOiByZXN1bHQuZXhpdENvZGUsXG5cdFx0XHR9O1xuXHRcdH1cblxuXHRcdGlmIChzdGRlcnJDb250ZW50ICYmIHN0ZGVyckNvbnRlbnQudHJpbSgpKSB7XG5cdFx0XHRyZXR1cm4ge1xuXHRcdFx0XHRzdWNjZXNzOiBmYWxzZSxcblx0XHRcdFx0ZGF0YTogdW5kZWZpbmVkLFxuXHRcdFx0XHRlcnJvcjogc3RkZXJyQ29udGVudCxcblx0XHRcdFx0ZXhpdENvZGU6IDAsXG5cdFx0XHR9O1xuXHRcdH1cblxuXHRcdGNvbnN0IG5vZGUgPSBmaWxlU3lzdGVtLmxvb2t1cCh0ZW1wRmlsZSk7XG5cdFx0aWYgKCFub2RlIHx8IG5vZGUudHlwZSAhPT0gXCJmaWxlXCIpIHtcblx0XHRcdHJldHVybiB7XG5cdFx0XHRcdHN1Y2Nlc3M6IGZhbHNlLFxuXHRcdFx0XHRkYXRhOiB1bmRlZmluZWQsXG5cdFx0XHRcdGVycm9yOiBgVGVtcG9yYXJ5IG91dHB1dCBmaWxlIG5vdCBmb3VuZDogJHt0ZW1wRmlsZX1gLFxuXHRcdFx0XHRleGl0Q29kZTogMCxcblx0XHRcdH07XG5cdFx0fVxuXG5cdFx0Y29uc3Qgb3V0cHV0RGF0YSA9XG5cdFx0XHRub2RlLmNvbnRlbnQgaW5zdGFuY2VvZiBCbG9iXG5cdFx0XHRcdD8gYXdhaXQgbm9kZS5jb250ZW50LmFycmF5QnVmZmVyKClcblx0XHRcdFx0OiAobm9kZS5jb250ZW50LmJ1ZmZlciBhcyBBcnJheUJ1ZmZlcik7XG5cblx0XHRyZXR1cm4ge1xuXHRcdFx0c3VjY2VzczogdHJ1ZSxcblx0XHRcdGRhdGE6IG91dHB1dERhdGEsXG5cdFx0XHRleGl0Q29kZTogMCxcblx0XHR9O1xuXHR9IGZpbmFsbHkge1xuXHRcdGNsZWFudXBUZW1wRmlsZXMoZmlsZVN5c3RlbSwgdGVtcEZpbGVzKTtcblx0fVxufVxuXG4vKipcbiAqIERpc3Bvc2Ugb2YgdGhlIGNhY2hlZCBaZXJvUGVybCBpbnN0YW5jZVxuICovXG5leHBvcnQgYXN5bmMgZnVuY3Rpb24gZGlzcG9zZSgpOiBQcm9taXNlPHZvaWQ+IHtcblx0Y29uc3QgY2FjaGVkUGVybCA9IGNhY2hlZFBlcmxSZWY/LmRlcmVmKCk7XG5cblx0aWYgKGNhY2hlZFBlcmwpIHtcblx0XHRjYWNoZWRQZXJsLmRpc3Bvc2UoKTtcblx0XHRjYWNoZWRQZXJsUmVmID0gbnVsbDtcblx0XHRjYWNoZWRGaWxlU3lzdGVtUmVmID0gbnVsbDtcblx0fVxufVxuIgogIF0sCiAgIm1hcHBpbmdzIjogIjRQQUFPLE1BQU0sQ0FBUSxPQUlELGVBQWdCLFFBS2hCLGlCQUFrQixRQUtsQixhQUFjLFNBS2QscUJBQXNCLFFBTXRCLHNCQUF1QixRQUt2QixrQkFBbUIsU0FJbkIsa0JBQW1CLFNBSW5CLG1CQUFvQixTQUlwQixrQkFBbUIsU0FJbkIsa0JBQW1CLFNBSW5CLGVBQWdCLFNBS2hCLGdDQUFpQyxRQUlqQyx5QkFBMEIsUUFJMUIsNEJBQTZCLFFBRzdCLGtCQUFtQixDQUMvQixXQUNBLGlCQUVBLGdCQUNBLGlCQUVBLGNBQ0Esb0JBRUEsWUFDQSxjQUNBLFdBQ0EsY0FDQSxnQkFDQSxzQkFDQSx1QkFD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Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7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QUFlLE1BQU0sR0FBWSxFQUFRLEtBQUssRUFDdEQsRUFBc0IsQ0FBQyxFQUU3QixFQUFPLE1BQU0sRUFDYixFQUFPLE1BQU0sRUFDYixNQUFNLEVBQUssTUFBTSxFQUVqQixHQUFJLENBQ0gsSUFBTSxFQUFZLElBQUksRUFBSyxPQUMzQixHQUFJLGFBQWdCLEtBQ25CLEVBQVcsUUFBUSxFQUFXLENBQUksRUFFbEMsT0FBVyxRQUFRLEVBQVcsRUFBSyxJQUFJLEVBRXhDLEVBQVUsS0FBSyxDQUFTLEVBS3hCLElBQU0sRUFBTyxDQUFDLEVBQ2QsR0FBSSxFQUFRLE9BQVEsQ0FDbkIsSUFBTSxFQUFhLElBQUksRUFBUSxPQUFPLE9BQ3RDLEdBQUksRUFBUSxrQkFBa0IsS0FDN0IsRUFBVyxRQUFRLEVBQVksRUFBUSxNQUFNLEVBRTdDLE9BQVcsUUFBUSxFQUFZLEVBQVEsT0FBTyxJQUFJLEVBRW5ELEVBQVUsS0FBSyxDQUFVLEVBQ3pCLEVBQUssS0FBSyxVQUFXLENBQVUsRUFFaEMsRUFBSyxLQUFLLEdBQUksRUFBUSxNQUFRLENBQUMsQ0FBRSxFQUVqQyxFQUFLLEtBQUssR0FBRyxHQUFjLENBQUksQ0FBQyxFQUVoQyxJQUFNLEVBQVcsSUFBSSxPQUFPLFdBQVcsRUFBRSxRQUFRLEtBQU0sRUFBRSxRQUN6RCxFQUFVLEtBQUssQ0FBUSxFQUV2QixFQUFLLEtBQUssS0FBTSxDQUFRLEVBQ3hCLEVBQUssS0FBSyxDQUFTLEVBRW5CLElBQU0sRUFBUyxNQUFNLEVBQUssUUFBUSxZQUFhLENBQUksRUFDbkQsRUFBSyxNQUFNLEVBRVgsSUFBTSxFQUFnQixFQUFPLFNBQVMsRUFFdEMsR0FBSSxDQUFDLEVBQU8sU0FBVyxFQUFPLFdBQWEsRUFHMUMsTUFBTyxDQUNOLFFBQVMsR0FDVCxLQUFNLE9BQ04sTUFMaUIsRUFBSyxhQUFhLEdBS2YsR0FBaUIsZ0JBQ3JDLFNBQVUsRUFBTyxRQUNsQixFQUdELEdBQUksR0FBaUIsRUFBYyxLQUFLLEVBQ3ZDLE1BQU8sQ0FDTixRQUFTLEdBQ1QsS0FBTSxPQUNOLE1BQU8sRUFDUCxTQUFVLENBQ1gsRUFHRCxJQUFNLEVBQU8sRUFBVyxPQUFPLENBQVEsRUFDdkMsR0FBSSxDQUFDLEdBQVEsRUFBSyxPQUFTLE9BQzFCLE1BQU8sQ0FDTixRQUFTLEdBQ1QsS0FBTSxPQUNOLE1BQU8sb0NBQW9DLElBQzNDLFNBQVUsQ0FDWCxFQVFELE1BQU8sQ0FDTixRQUFTLEdBQ1QsS0FOQSxFQUFLLG1CQUFtQixLQUNyQixNQUFNLEVBQUssUUFBUSxZQUFZLEVBQzlCLEVBQUssUUFBUSxPQUtqQixTQUFVLENBQ1gsU0FDQyxDQUNELEdBQWlCLEVBQVksQ0FBUyxHQU94QyxlQUFzQixFQUFPLEVBQWtCLENBQzlDLElBQU0sRUFBYSxHQUFlLE1BQU0sRUFFeEMsR0FBSSxFQUNILEVBQVcsUUFBUSxFQUNuQixFQUFnQixLQUNoQixFQUFzQiIsCiAgImRlYnVnSWQiOiAiN0IwMjgwRDJBQzM1RTVFRTY0NzU2RTIxNjQ3NTZFMjEiLAogICJuYW1lcyI6IFtdCn0=
