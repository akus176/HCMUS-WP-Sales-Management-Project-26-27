import http from 'node:http';
import net from 'node:net';
import fs from 'node:fs';
import path from 'node:path';
const root=path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1'));
const logs=process.argv[2];
for (const [client,port] of [['winui',52300],['android',52301]]) {
 const record=(method,url,status)=>fs.appendFileSync(path.join(logs,`${client}-requests.jsonl`),JSON.stringify({at:new Date().toISOString(),client,method,path:new URL(url,'http://127.0.0.1').pathname,status})+'\n');
 const server=http.createServer((req,res)=>{
  const upstream=http.request({hostname:'127.0.0.1',port:52302,method:req.method,path:req.url,headers:{...req.headers,host:'127.0.0.1:52302'}},reply=>{record(req.method,req.url,reply.statusCode);res.writeHead(reply.statusCode,reply.headers);reply.pipe(res);});
  upstream.on('error',()=>{record(req.method,req.url,502);res.writeHead(502);res.end();});
  req.on('aborted',()=>upstream.destroy()); req.pipe(upstream);
 });
 server.on('upgrade',(req,socket,head)=>{
  const upstream=net.connect(52302,'127.0.0.1',()=>{
   let headers=`${req.method} ${req.url} HTTP/${req.httpVersion}\r\n`;
   for(const [key,value] of Object.entries(req.headers)) headers+=`${key}: ${key==='host'?'127.0.0.1:52302':value}\r\n`;
   upstream.write(headers+'\r\n');if(head.length)upstream.write(head);socket.pipe(upstream).pipe(socket);
   record(req.method,req.url,'upgrade');
  });
  upstream.on('error',()=>socket.destroy());socket.on('error',()=>upstream.destroy());socket.on('close',()=>upstream.destroy());
 });
 server.listen(port,'127.0.0.1',()=>console.log(`${client} trace listener ready on ${port}`));
}
