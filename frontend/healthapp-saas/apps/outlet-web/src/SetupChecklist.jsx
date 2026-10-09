import React,{useEffect,useState}from'react';
import{outletAdmin}from'@healthapp/shared';

const KEY='healthe.setupChecklist.dismissed';
const readDismissed=()=>{try{return localStorage.getItem(KEY)==='1'}catch{return false}};

/* Guided first-run checklist for new outlets. Counts come from existing outlet APIs;
   an item whose request fails is left out rather than shown as incomplete. */
export default function SetupChecklist({recipes,pricing,nav}){
 const [data,setData]=useState(null);
 const [dismissed,setDismissed]=useState(readDismissed);
 useEffect(()=>{
  let live=true;
  Promise.allSettled([outletAdmin.menu(),outletAdmin.selectedDeliveryAreas(),outletAdmin.discountTiers(),outletAdmin.drivers()])
   .then(r=>{if(live)setData(r.map(x=>x.status==='fulfilled'&&Array.isArray(x.value)?x.value.length:null))});
  return()=>{live=false};
 },[]);
 if(dismissed||!data)return null;
 const all=[
  {id:'recipes',label:'Add your recipes',count:Array.isArray(recipes)?recipes.length:null,go:'recipes'},
  {id:'menu',label:'Build the weekly menu',count:data[0],go:'menu'},
  {id:'areas',label:'Choose delivery areas',count:data[1],go:'delivery-areas'},
  {id:'pricing',label:'Set delivery pricing',count:Array.isArray(pricing)?pricing.length:null,go:'pricing'},
  {id:'discounts',label:'Set package discounts',count:data[2],go:'discounts'},
  {id:'drivers',label:'Add a delivery driver',count:data[3],go:'routes'}
 ].filter(x=>x.count!==null);
 const done=all.filter(x=>x.count>0).length;
 if(!all.length||done===all.length)return null;
 const pct=Math.round(done/all.length*100);
 const dismiss=()=>{try{localStorage.setItem(KEY,'1')}catch{}setDismissed(true)};
 return <section className="panel setupChecklist" aria-labelledby="setup-title">
  <div className="panelHead"><div><h3 id="setup-title">Set up your outlet</h3><p>{done} of {all.length} done. Finish these so customers can order.</p></div><button type="button" className="linkBtn" onClick={dismiss}>Hide</button></div>
  <div className="setupBar" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow={pct}><span style={{width:pct+'%'}}/></div>
  <ul className="setupList">{all.map(x=><li key={x.id} className={x.count>0?'done':''}>
   <span className="setupCheck" aria-hidden="true">{x.count>0?'✓':''}</span>
   <span className="setupLabel">{x.label}</span>
   {x.count>0?<span className="pill green">Done</span>:<button type="button" className="secondary smallBtn" onClick={()=>nav(x.go)}>Set up</button>}
  </li>)}</ul>
 </section>;
}
