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
    // A click trap is a viewport-sized fixed/absolute layer with no content of its own that swallows the click and
    // opens an ad: invisible (opacity 0), at an extreme z-index, or an empty link to another site. A merely
    // transparent layer at an ordinary z-index is NOT treated as a trap: UI toolkits use those as click-away
    // backdrops. The cursor click goes to the element under a trap. Any failure to inspect an element means
    // "not a trap": clicking normally is the safe default.
    private const string Target = @"const w0=window;let d=document,w=window,x=FX*innerWidth,y=FY*innerHeight,e=null;
        let site0='';try{site0=w0.location.hostname.split('.').slice(-2).join('.')}catch(_){}
        const own=u=>{try{return new URL(u,w0.location.href).hostname.endsWith(site0)}catch(_){return true}};
        const trap=(n,v)=>{try{
          if(!n.tagName||/^(IFRAME|FRAME|VIDEO|AUDIO|EMBED|OBJECT|CANVAS|HTML|BODY)$/.test(n.tagName))return false;
          const s=v.getComputedStyle(n);if(s.position!=='fixed'&&s.position!=='absolute')return false;
          const r=n.getBoundingClientRect(),big=r.width>=v.innerWidth*.8&&r.height>=v.innerHeight*.8;
          // A smaller layer counts only when it is both invisible and at an extreme z-index (a ghost over part of the page).
          const ghost=Number(s.opacity)<.05&&Number(s.zIndex)>=2147483000&&r.width>=100&&r.height>=100;
          if(!big&&!ghost)return false;
          if((n.textContent||'').trim()||n.getElementsByTagName('*').length>3||
            n.querySelector('img,video,canvas,svg,iframe,input,button,audio'))return false;
          return ghost||Number(s.opacity)<.05||Number(s.zIndex)>=2147483000||(n.tagName==='A'&&!!n.href&&!own(n.href))
        }catch(_){return false}};
        const pick=(dd,v,px,py)=>{const l=dd.elementsFromPoint?dd.elementsFromPoint(px,py):[dd.elementFromPoint(px,py)];
          for(const n of l)if(n&&!trap(n,v))return n;return l[0]||null};
        for(let k=0;k<6;k++){e=pick(d,w,x,y);if(!e)return;
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
    // Ad interstitials and click traps sit on a body-level layer that covers the viewport at the maximum
    // z-index, often inside a shadow root. Such a layer is hidden and removed (which also frees its decoder),
    // unless it holds a form or first-party content. The observer only watches direct children of body.
    private const string SweepBody = @"
        const BIG=2147483000,site=location.hostname.split('.').slice(-2).join('.');
        // blob: URLs have no host; their origin says which page made them (MSE players use them).
        const mine=u=>{try{const x=new URL(u,location.href);let h=x.hostname;if(!h)h=new URL(x.origin).hostname;return h.endsWith(site)}catch(_){return false}};
        const big=n=>{const s=getComputedStyle(n);if(s.position!=='fixed'||!(Number(s.zIndex)>=BIG))return false;
          const r=n.getBoundingClientRect();return r.width>=innerWidth*.8&&r.height>=innerHeight*.8};
        const clean=n=>!n.querySelector('input,textarea,select')&&
          ![...n.querySelectorAll('iframe,video,source')].some(f=>mine(f.currentSrc||f.src||''));
        const layer=n=>{if(n.nodeType!==1)return null;if(big(n)&&clean(n))return n;
          const kids=n.shadowRoot?[...n.shadowRoot.children]:[];
          for(const c of kids)if(c.nodeType===1&&big(c)&&clean(c))return n;return null};
        const sweep=()=>{if(!window.__iskoSweepOn||document.fullscreenElement)return;
          for(const n of [...document.body.children]){let t=null;try{t=layer(n)}catch(_){}
            if(t){t.style.setProperty('display','none','important');t.remove();window.__iskoSwept=(window.__iskoSwept||0)+1}}};
        if(!window.__iskoSweep){window.__iskoSweep=true;new MutationObserver(sweep).observe(document.body,{childList:true})}
        sweep();";
    public static string SweepOverlays(bool on) => "(()=>{window.__iskoSweepOn=" + (on ? "true" : "false") + ";" + SweepBody + "})()";
    // Page-side supplement to the native refusal: a download link is never followed, however it is triggered.
    public const string BlockDownloadLinks = @"(()=>{if(window.__iskoDl)return;window.__iskoDl=true;
        document.addEventListener('click',e=>{const a=e.target&&e.target.closest&&e.target.closest('a[download]');
          if(a){e.preventDefault();e.stopImmediatePropagation()}},true);
        const c=HTMLAnchorElement.prototype.click;
        HTMLAnchorElement.prototype.click=function(){if(this.hasAttribute('download'))return;return c.apply(this,arguments)}})()";
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
          jsHeapBytes:performance.memory?performance.memory.usedJSHeapSize:null,sweptLayers:window.__iskoSwept??null};
    })())";
}
