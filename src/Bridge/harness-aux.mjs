import {readFile} from 'node:fs/promises';
import {setTimeout as delay} from 'node:timers/promises';
export const inject = ['deepseekAccount','llm'];
export async function apply(ctx) {
  try {
    let result;
    const mode=process.env.TETO_AUX_MODE;
    if(mode==='balance') result=await ctx.deepseekAccount.getBalance({version:'0.1.18',locale:'zh_CN',timezoneOffsetSeconds:28800});
    else if(mode==='catalog') {
      result=await Promise.all((await ctx.llm.listModels('deepseek-account')).map(async m=>{
        const info=await ctx.llm.resolveModelInfo('deepseek-account',m.id);
        return {id:m.id,name:m.name,inputModalities:info.inputModalities,reasoning:info.reasoning};
      }));
    } else if(mode==='audio'||mode==='audio-prepare') {
      const speech=ctx.speechToText;
      let provider;
      for(let i=0;i<100;i++){provider=speech.snapshot().providers.find(p=>p.id==='sensevoice-local');if(provider)break;await delay(100);}
      if(!provider) throw new Error('本地语音识别插件不可用');
      if(!['ready','standby'].includes(provider.preparation.phase)) speech.prepare('sensevoice-local');
      const deadline=Date.now()+10*60*1000;
      while(true){
        provider=speech.snapshot().providers.find(p=>p.id==='sensevoice-local');
        if(['ready','standby'].includes(provider?.preparation.phase))break;
        if(provider?.preparation.phase==='failed')throw new Error('本地语音模型准备失败：'+provider.preparation.message);
        if(Date.now()>deadline)throw new Error('本地语音模型准备超时');
        await delay(500);
      }
      if(mode==='audio-prepare')result={ready:true};
      else {
        const audio=new Uint8Array(await readFile(process.env.TETO_AUDIO_FILE));
        const specification=speech.resolve({providerId:'sensevoice-local',audio,language:'auto'});
        result=await speech.transcribe(specification,AbortSignal.timeout(120000));
      }
    } else throw new Error('不支持的辅助操作');
    process.stdout.write(JSON.stringify({result})+'\n');
  } catch(error) {
    // No raw account response, headers, credentials, or stack enters the UI.
    const mode=process.env.TETO_AUX_MODE;
    process.stdout.write(JSON.stringify({error:mode?.startsWith('audio')?(error.message?.startsWith('本地')?error.message:'本地语音转写失败，请检查音频格式和模型资源。'):'Harness 账号查询失败，请检查登录与网络。'})+'\n');
  }
  setTimeout(async()=>{await ctx.root.dispose();process.exit(0);},30);
}
