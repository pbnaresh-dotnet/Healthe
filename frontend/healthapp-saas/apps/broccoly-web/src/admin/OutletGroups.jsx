import React,{useState}from'react';
import{admin}from'@healthapp/shared';

const tone=s=>String(s||'').toLowerCase().includes('active')?'success':String(s||'').toLowerCase().includes('disabled')?'danger':'neutral';
const Pill=({status})=><span className={'statusPill '+tone(status)}>{status||'Unknown'}</span>;

export default function OutletGroups({groups=[],onSave}){
 const[editing,setEditing]=useState(null);
 const[form,setForm]=useState({name:'',description:'',isActive:true,sortOrder:groups.length+1});
 const[busy,setBusy]=useState(false);
 const[error,setError]=useState('');
 const reset=()=>{setEditing(null);setError('');setForm({name:'',description:'',isActive:true,sortOrder:groups.length+1})};
 const edit=g=>{setEditing(g.id);setError('');setForm({name:g.name,description:g.description||'',isActive:g.isActive,sortOrder:g.sortOrder||0})};
 const save=async()=>{if(!form.name.trim())return;try{setBusy(true);setError('');await onSave({...form,name:form.name.trim(),description:form.description.trim()},editing);reset()}catch(e){setError(e.message||'Unable to save group')}finally{setBusy(false)}};
 return <section>
  <div className="pageIntro"><div><span className="eyebrow">TENANT SEGMENTATION</span><h1>Outlet groups</h1><p>Create stable tenant cohorts such as region, franchise, commercial tier or operating segment. Groups are for reporting and operations, not tenant security.</p></div></div>
  <div className="groupLayout">
   <section className="card"><div className="cardHead"><div><span className="eyebrow">REPORTING COHORTS</span><h2>Groups</h2><p>Inactive groups remain historical but cannot receive new outlets.</p></div><span className="countBadge">{groups.length}</span></div>
    <div className="groupList">{groups.map(g=><article className="groupCard" key={g.id}><div className="groupBadge">▦</div><div className="groupInfo"><h3>{g.name}</h3><p>{g.description||'No description'}</p><small>{g.outletCount} outlet{g.outletCount===1?'':'s'} · sort {g.sortOrder}</small></div><Pill status={g.isActive?'Active':'Disabled'}/><button className="secondaryBtn compactBtn" type="button" onClick={()=>edit(g)}>Edit</button></article>)}{!groups.length&&<div className="emptyState">No outlet groups yet. Create the first reporting cohort.</div>}</div>
   </section>
   <section className="card groupForm"><div className="cardHead"><div><span className="eyebrow">{editing?'EDIT GROUP':'NEW GROUP'}</span><h2>{editing?'Update group':'Create outlet group'}</h2><p>Keep names stable because saved reports will eventually use these groups as dimensions.</p></div></div>
    <div className="formGrid"><label>Group name<input value={form.name} onChange={e=>setForm({...form,name:e.target.value})} placeholder="e.g. South Region"/></label><label>Description<input value={form.description} onChange={e=>setForm({...form,description:e.target.value})} placeholder="Optional description"/></label><label>Sort order<input type="number" value={form.sortOrder} onChange={e=>setForm({...form,sortOrder:Number(e.target.value)})}/></label><label className="checkLine"><input type="checkbox" checked={form.isActive} onChange={e=>setForm({...form,isActive:e.target.checked})}/> Active and available for assignment</label></div>
    {error&&<div className="errorBanner">{error}</div>}<div className="formActions"><button className="secondaryBtn" type="button" onClick={reset}>Clear</button><button className="primaryBtn" type="button" disabled={busy||!form.name.trim()} onClick={save}>{busy?'Saving…':editing?'Save changes':'Create group'}</button></div>
   </section>
  </div>
 </section>;
}