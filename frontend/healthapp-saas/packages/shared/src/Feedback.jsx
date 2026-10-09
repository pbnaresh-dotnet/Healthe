import React,{createContext,useCallback,useContext,useEffect,useId,useRef,useState} from 'react';

const FeedbackContext=createContext(null);
let feedbackSequence=0;

export function AppFeedbackProvider({children}){
  const [items,setItems]=useState([]);
  const [confirmation,setConfirmation]=useState(null);
  const timers=useRef(new Map());
  const confirmResolver=useRef(null);
  const notify=useCallback((message,type='success',options={})=>{
    if(!message)return;
    const id=++feedbackSequence;
    const item={id,message:String(message),type:['success','error','warning','info'].includes(type)?type:'info',duration:options.duration??4200};
    setItems(current=>[...current,item].slice(-4));
    if(item.duration>0){
      const timer=setTimeout(()=>{setItems(current=>current.filter(x=>x.id!==id));timers.current.delete(id)},item.duration);
      timers.current.set(id,timer);
    }
    return id;
  },[]);
  const dismiss=useCallback(id=>{const timer=timers.current.get(id);if(timer)clearTimeout(timer);timers.current.delete(id);setItems(current=>current.filter(x=>x.id!==id))},[]);
  const confirm=useCallback(options=>new Promise(resolve=>{
    if(confirmResolver.current)confirmResolver.current(false);
    confirmResolver.current=resolve;
    setConfirmation({title:options?.title||'Please confirm',message:options?.message||'Are you sure you want to continue?',confirmLabel:options?.confirmLabel||'Continue',cancelLabel:options?.cancelLabel||'Cancel',variant:options?.variant==='danger'?'danger':'default'});
  }),[]);
  const finishConfirm=useCallback(value=>{setConfirmation(null);const resolve=confirmResolver.current;confirmResolver.current=null;resolve?.(value)},[]);
  useEffect(()=>()=>{timers.current.forEach(clearTimeout);timers.current.clear();confirmResolver.current?.(false)},[]);
  const titleId=useId();
  useEffect(()=>{if(!confirmation)return;const onKey=e=>{if(e.key==='Escape'){e.preventDefault();finishConfirm(false)}};document.addEventListener('keydown',onKey);return()=>document.removeEventListener('keydown',onKey)},[confirmation,finishConfirm]);
  return <FeedbackContext.Provider value={{notify,confirm}}>
    {children}
    <div className="ha-feedbackViewport" aria-live="polite" aria-relevant="additions text">
      {items.map(item=><div className={'ha-toast ha-toast-'+item.type} role={item.type==='error'?'alert':'status'} key={item.id}>
        <span className="ha-toastMark" aria-hidden="true">{item.type==='success'?'✓':item.type==='error'?'!':item.type==='warning'?'⚠':'i'}</span>
        <div className="ha-toastText">{item.message}</div>
        <button type="button" className="ha-toastClose" aria-label="Dismiss notification" onClick={()=>dismiss(item.id)}>×</button>
      </div>)}
    </div>
    {confirmation&&<div className="ha-dialogBackdrop" role="presentation" onMouseDown={e=>e.target===e.currentTarget&&finishConfirm(false)}>
      <section className={'ha-dialog '+(confirmation.variant==='danger'?'ha-dialogDanger':'')} role="alertdialog" aria-modal="true" aria-labelledby={titleId}>
        <div className="ha-dialogIcon" aria-hidden="true">{confirmation.variant==='danger'?'!':'?'}</div>
        <div className="ha-dialogContent"><h2 id={titleId}>{confirmation.title}</h2><p>{confirmation.message}</p>
          <div className="ha-dialogActions"><button type="button" className="ha-dialogCancel" onClick={()=>finishConfirm(false)}>{confirmation.cancelLabel}</button><button type="button" className={'ha-dialogConfirm '+(confirmation.variant==='danger'?'danger':'')} onClick={()=>finishConfirm(true)}>{confirmation.confirmLabel}</button></div>
        </div>
      </section>
    </div>}
  </FeedbackContext.Provider>;
}

export function useFeedback(){
  const value=useContext(FeedbackContext);
  if(!value)throw new Error('useFeedback must be used inside AppFeedbackProvider');
  return value;
}

export function AppModal({title,onClose,children,wide=false,backdropClass='modalBackdrop',modalClass='modal',bodyClass='modalBody',showClose=true}){
  const titleId=useId(),closeRef=useRef(null),previousFocus=useRef(null),onCloseRef=useRef(onClose);
  onCloseRef.current=onClose;
  useEffect(()=>{
    previousFocus.current=document.activeElement;
    const oldOverflow=document.body.style.overflow;
    document.body.style.overflow='hidden';
    const frame=requestAnimationFrame(()=>closeRef.current?.focus());
    const onKey=e=>{if(e.key==='Escape'){e.preventDefault();e.stopPropagation();onCloseRef.current?.()}};
    document.addEventListener('keydown',onKey);
    return()=>{cancelAnimationFrame(frame);document.removeEventListener('keydown',onKey);document.body.style.overflow=oldOverflow;const target=previousFocus.current;if(target&&document.contains(target)&&typeof target.focus==='function')requestAnimationFrame(()=>target.focus())};
  },[]);
  return <div className={backdropClass} role="presentation" onMouseDown={e=>e.target===e.currentTarget&&onClose?.()}>
    <div className={[modalClass,wide?'wide':''].filter(Boolean).join(' ')} role="dialog" aria-modal="true" aria-labelledby={titleId}>
      <div className="modalHead"><h3 id={titleId}>{title}</h3>{showClose&&<button ref={closeRef} type="button" className="iconBtn" onClick={onClose} aria-label={'Close '+title}>×</button>}</div>
      {bodyClass?<div className={bodyClass}>{children}</div>:children}
    </div>
  </div>;
}
