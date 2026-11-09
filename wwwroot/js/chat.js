(function(){
    const root=document.querySelector("[data-conversation-id]");
    if(!root||!window.ZuSignalR)return;
    const conversationId=Number(root.dataset.conversationId);
    const currentUserId=root.dataset.userId;
    const messages=root.querySelector("[data-chat-messages]");
    const empty=messages.querySelector("[data-chat-empty]");
    const form=root.querySelector("[data-chat-form]");
    const input=form.querySelector("textarea");
    const status=root.querySelector("[data-chat-status]");
    const retry=root.querySelector("[data-chat-retry]");
    const error=root.querySelector("[data-chat-error]");
    const connection=new ZuSignalR.Connection("/chat-hub");
    const labels={connecting:root.dataset.connecting,connected:root.dataset.connected,disconnected:root.dataset.disconnected,sendFailed:root.dataset.sendFailed};
    function updateState(state){status.textContent=labels[state]||labels.disconnected;status.dataset.state=state;retry.hidden=state!=="disconnected";if(state==="connected")connection.invoke("JoinConversation",conversationId).catch(()=>updateState("disconnected"))}
    function appendMessage(message){
        if(messages.querySelector(`[data-message-id="${message.id}"]`))return;
        empty?.remove();
        const wrapper=document.createElement("div");
        wrapper.className=`message ${message.senderId===currentUserId?"mine":"theirs"}`;
        wrapper.dataset.messageId=message.id;
        const body=document.createElement("p");
        body.textContent=message.body;
        const time=document.createElement("time");
        time.dateTime=message.sentAtUtc;
        time.textContent=new Date(message.sentAtUtc).toLocaleString(document.documentElement.lang);
        wrapper.append(body,time);
        messages.appendChild(wrapper);
        messages.scrollTop=messages.scrollHeight;
    }
    connection.on("ReceiveMessage",appendMessage);
    connection.onstatechange=updateState;
    retry.addEventListener("click",()=>connection.start().catch(()=>updateState("disconnected")));
    form.addEventListener("submit",async event=>{
        event.preventDefault();error.hidden=true;
        const body=input.value.trim();if(!body)return;
        const button=form.querySelector("button[type=submit]");button.disabled=true;
        try{
            const response=await fetch(form.action,{method:"POST",body:new FormData(form),credentials:"same-origin",headers:{"X-Requested-With":"XMLHttpRequest","Accept":"application/json"}});
            if(!response.ok){const payload=await response.json().catch(()=>({}));throw new Error(payload.error||"send")}
            const payload=await response.json();appendMessage(payload);input.value="";
        }catch{error.textContent=labels.sendFailed;error.hidden=false}
        finally{button.disabled=false;input.focus()}
    });
    messages.scrollTop=messages.scrollHeight;
    connection.start().catch(()=>updateState("disconnected"));
    window.addEventListener("beforeunload",()=>connection.stop());
})();
