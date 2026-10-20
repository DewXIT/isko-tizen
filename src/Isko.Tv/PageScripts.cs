namespace Isko.Tv;

// Original fixed scripts bundled in the application. No remotely executed code.
internal static class PageScripts
{
    public const string PauseMedia = @"(()=>{document.querySelectorAll('video,audio').forEach(v=>v.pause())})()";
    public const string ReleaseMedia = @"(()=>{document.querySelectorAll('video,audio').forEach(v=>{v.pause();v.removeAttribute('src');v.querySelectorAll('source').forEach(s=>s.removeAttribute('src'));v.load()})})()";
    public const string ObserveMedia = @"(()=>{
        // Observe only. Preview/live-player handoffs belong to the site, not the shell.
        if(window.__iskoMedia)return;
        const s=window.__iskoMedia={errors:0,waiting:0,stalls:0};
        document.addEventListener('error',e=>{if(e.target instanceof HTMLMediaElement)s.errors++},true);
        document.addEventListener('waiting',e=>{if(e.target instanceof HTMLMediaElement)s.waiting++},true);
        document.addEventListener('stalled',e=>{if(e.target instanceof HTMLMediaElement)s.stalls++},true);
    })()";
    // Remote cursor. FX/FY are the cursor's fraction of the viewport, so device pixel ratio
    // never matters. Same-origin frames and open shadow roots are entered; a cross-origin
    // frame receives nothing, because no public API injects real touch input.
    private const string Target = @"const w0=window;let d=document,w=window,x=FX*innerWidth,y=FY*innerHeight,e=null;
        for(let k=0;k<6;k++){e=d.elementFromPoint(x,y);if(!e)return;
          while(e.shadowRoot){const i=e.shadowRoot.elementFromPoint(x,y);if(!i||i===e)break;e=i}
          if(e.tagName!=='IFRAME'&&e.tagName!=='FRAME')break;
          let c=null;try{c=e.contentDocument}catch(_){}if(!c||k===5)return;
          const r=e.getBoundingClientRect();x-=r.left;y-=r.top;d=c;w=c.defaultView||w0}";
    private const string Click = @"(()=>{" + Target + @"
        if(e.closest&&e.closest(':disabled,[inert]'))return;
        const o={bubbles:true,cancelable:true,composed:true,view:w,clientX:x,clientY:y,detail:1,button:0,buttons:1,pointerId:1,pointerType:'mouse',isPrimary:true};
        const P=w.PointerEvent||MouseEvent,M=w.MouseEvent||MouseEvent;
        e.dispatchEvent(new P('pointerdown',o));e.dispatchEvent(new M('mousedown',o));
        if(e.focus)e.focus({preventScroll:true});o.buttons=0;
        e.dispatchEvent(new P('pointerup',o));e.dispatchEvent(new M('mouseup',o));
        // HTMLElement.click() loses the hit coordinates, breaking coordinate-based players.
        // This preserves coordinates, but remains untrusted and cannot grant user activation.
        e.dispatchEvent(new M('click',o));
    })()";
    // DY/DX are viewport fractions. The nearest scrollable box under the cursor moves first,
    // then the document, like a mouse wheel.
    private const string Scroll = @"(()=>{" + Target + @"
        const dx=DX*w.innerWidth,dy=DY*w.innerHeight;
        for(let n=e;n&&n!==d.body&&n!==d.documentElement;n=n.parentElement){const s=w.getComputedStyle(n);
          if(dy&&/(auto|scroll)/.test(s.overflowY)&&n.scrollHeight>n.clientHeight){const t=n.scrollTop;n.scrollBy(0,dy);if(n.scrollTop!==t)return}
          if(dx&&/(auto|scroll)/.test(s.overflowX)&&n.scrollWidth>n.clientWidth){const t=n.scrollLeft;n.scrollBy(dx,0);if(n.scrollLeft!==t)return}}
        (d.scrollingElement||d.documentElement).scrollBy(dx,dy);
    })()";
    private static string Number(float value) => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    public static string ClickAt(float fx, float fy) => Click.Replace("FX", Number(fx)).Replace("FY", Number(fy));
    public static string ScrollAt(float fx, float fy, float dx, float dy) =>
        Scroll.Replace("FX", Number(fx)).Replace("FY", Number(fy)).Replace("DX", Number(dx)).Replace("DY", Number(dy));
    public const string ReadMedia = @"JSON.stringify((()=>{
        const s=window.__iskoMedia||{},videos=Array.from(document.querySelectorAll('video'));
        // The first video may be a hidden preview. Prefer active media, then visible area.
        const area=v=>{const r=v.getBoundingClientRect(),c=getComputedStyle(v);return c.display==='none'||c.visibility==='hidden'?0:
          Math.max(0,Math.min(innerWidth,r.right)-Math.max(0,r.left))*Math.max(0,Math.min(innerHeight,r.bottom)-Math.max(0,r.top))};
        const active=v=>!v.paused&&!v.ended;
        const v=videos.reduce((best,item)=>!best||Number(active(item))>Number(active(best))||
          (active(item)===active(best)&&area(item)>area(best))?item:best,null);
        const q=v&&typeof v.getVideoPlaybackQuality==='function'?v.getVideoPlaybackQuality():null;
        const r=v?v.getBoundingClientRect():null;
        let ahead=null;try{if(v)for(let i=0;i<v.buffered.length;i++)if(v.buffered.start(i)<=v.currentTime&&v.buffered.end(i)>=v.currentTime){ahead=v.buffered.end(i)-v.currentTime;break}}catch(_){}
        return {errors:s.errors??null,waiting:s.waiting??null,stalls:s.stalls??null,
          videoCount:videos.length,activeVideos:videos.filter(active).length,
          readyState:v?v.readyState:null,networkState:v?v.networkState:null,mediaError:v&&v.error?v.error.code:null,
          paused:v?Number(v.paused):null,currentTime:v?v.currentTime:null,bufferAhead:ahead,
          videoWidth:v?v.videoWidth:null,videoHeight:v?v.videoHeight:null,
          boxWidth:r?r.width:null,boxHeight:r?r.height:null,
          droppedFrames:q?q.droppedVideoFrames:null,totalFrames:q?q.totalVideoFrames:null,
          jsHeapBytes:performance.memory?performance.memory.usedJSHeapSize:null};
    })())";
}
