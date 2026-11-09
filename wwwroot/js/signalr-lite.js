(function(){
    const separator=String.fromCharCode(30);
    class Connection{
        constructor(url){this.url=url;this.socket=null;this.handlers=new Map();this.pending=new Map();this.sequence=0;this.closed=false;this.reconnectAttempt=0;this.onstatechange=null}
        on(name,handler){const list=this.handlers.get(name)||[];list.push(handler);this.handlers.set(name,list)}
        async start(){this.closed=false;await this.connect()}
        async connect(){
            this.setState("connecting");
            const response=await fetch(`${this.url}/negotiate?negotiateVersion=1`,{method:"POST",credentials:"same-origin",headers:{"X-Requested-With":"XMLHttpRequest"}});
            if(!response.ok)throw new Error("negotiate");
            const negotiation=await response.json();
            const scheme=location.protocol==="https:"?"wss:":"ws:";
            const path=this.url.startsWith("http")?new URL(this.url):new URL(this.url,location.origin);
            const socketUrl=`${scheme}//${path.host}${path.pathname}?id=${encodeURIComponent(negotiation.connectionToken)}`;
            await new Promise((resolve,reject)=>{
                let handshaken=false;
                this.socket=new WebSocket(socketUrl);
                this.socket.onopen=()=>this.socket.send(JSON.stringify({protocol:"json",version:1})+separator);
                this.socket.onerror=()=>{if(!handshaken)reject(new Error("socket"))};
                this.socket.onmessage=event=>{
                    for(const frame of event.data.split(separator).filter(Boolean)){
                        const message=JSON.parse(frame);
                        if(!handshaken){handshaken=true;this.reconnectAttempt=0;this.setState("connected");resolve();continue}
                        this.receive(message);
                    }
                };
                this.socket.onclose=()=>{if(!handshaken)reject(new Error("closed"));this.setState("disconnected");if(!this.closed)this.reconnect()};
            });
        }
        receive(message){
            if(message.type===1){for(const handler of this.handlers.get(message.target)||[])handler(...(message.arguments||[]));return}
            if(message.type===3&&message.invocationId){const pending=this.pending.get(message.invocationId);if(!pending)return;this.pending.delete(message.invocationId);message.error?pending.reject(new Error(message.error)):pending.resolve(message.result)}
        }
        invoke(target,...args){
            if(!this.socket||this.socket.readyState!==WebSocket.OPEN)return Promise.reject(new Error("disconnected"));
            const invocationId=String(++this.sequence);
            this.socket.send(JSON.stringify({type:1,invocationId,target,arguments:args})+separator);
            return new Promise((resolve,reject)=>{this.pending.set(invocationId,{resolve,reject});setTimeout(()=>{if(this.pending.delete(invocationId))reject(new Error("timeout"))},10000)})
        }
        async reconnect(){this.reconnectAttempt++;await new Promise(resolve=>setTimeout(resolve,Math.min(1000*this.reconnectAttempt,10000)));if(!this.closed)this.connect().catch(()=>this.reconnect())}
        setState(state){if(this.onstatechange)this.onstatechange(state)}
        stop(){this.closed=true;this.socket?.close()}
    }
    window.ZuSignalR={Connection};
})();
