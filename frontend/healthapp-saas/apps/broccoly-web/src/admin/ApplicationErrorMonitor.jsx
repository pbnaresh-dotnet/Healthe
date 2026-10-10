import React,{useEffect,useState}from'react';
import{admin}from'@healthapp/shared';

export default function ApplicationErrorMonitor({outlets=[]}){
 const[monitor,setMonitor]=useState({items:[],totalCount:0,page:1,pageSize:50,summary:{totalCount:0,unresolvedCount:0,last24HoursCount:0,byOutlet:[]}});
 const[filters,setFilters]=useState({outletId:'',severity:'',statusCode:'',resolved:'false',search:''});
 const[selected,setSelected]=useState(null);
 const[resolutionNotes,setResolutionNotes]=useState('');
 const[loading,setLoading]=useState(false);
 const[page,setPage]=useState(1);
 const[pageSize,setPageSize]=useState(50);
 const[error,setError]=useState('');
 const[diagnostics,setDiagnostics]=useState({requestLoggingEnabled:true,detailedLoggingEnabled:false,slowRequestThresholdMs:1000});
 const[settingsBusy,setSettingsBusy]=useState(false);
 const[settingsMessage,setSettingsMessage]=useState('');

 const load=async(next=filters,nextPage=page)=>{
   try{
     setLoading(true);setError('');
     const result=await admin.errors({
       outletId:next.outletId||undefined,
       severity:next.severity||undefined,
       statusCode:next.statusCode||undefined,
       resolved:next.resolved===''?undefined:next.resolved,
       search:next.search||undefined,
       page:nextPage,
       pageSize
     });
     setMonitor(result||{items:[],totalCount:0,page:1,pageSize:50,summary:{totalCount:0,unresolvedCount:0,last24HoursCount:0,byOutlet:[]}});
   }catch(e){setError(e.message||'Unable to load application errors')}finally{setLoading(false)}
 };

 useEffect(()=>{load(filters,1)},[outlets.length]);
 useEffect(()=>{load(filters,page)},[page,pageSize]);
 useEffect(()=>{let active=true;admin.diagnosticsSettings().then(value=>{if(active&&value)setDiagnostics({requestLoggingEnabled:!!value.requestLoggingEnabled,detailedLoggingEnabled:!!value.detailedLoggingEnabled,slowRequestThresholdMs:Number(value.slowRequestThresholdMs||1000)})}).catch(e=>{if(active)setSettingsMessage(e.message||'Unable to load diagnostics settings')});return()=>{active=false}},[]);
 const saveDiagnostics=async()=>{try{setSettingsBusy(true);setSettingsMessage('');const value=await admin.updateDiagnosticsSettings(diagnostics);setDiagnostics({requestLoggingEnabled:!!value.requestLoggingEnabled,detailedLoggingEnabled:!!value.detailedLoggingEnabled,slowRequestThresholdMs:Number(value.slowRequestThresholdMs||1000)});setSettingsMessage('Diagnostics settings saved. Detailed exception information is sensitive; enable it only while investigating and turn it off afterwards.')}catch(e){setSettingsMessage(e.message||'Unable to save diagnostics settings')}finally{setSettingsBusy(false)}};

 const apply=next=>{setFilters(next);setPage(1);load(next,1)};
 const open=async id=>{
   try{setLoading(true);setError('');setSelected(await admin.error(id));setResolutionNotes('')}
   catch(e){setError(e.message||'Unable to load error details')}
   finally{setLoading(false)}
 };
 const resolve=async()=>{
   if(!selected)return;
   try{
     setLoading(true);setError('');
     const item=await admin.resolveError(selected.id,resolutionNotes);
     setSelected(item);
     await load(filters,page);
   }catch(e){setError(e.message||'Unable to resolve error')}finally{setLoading(false)}
 };

 return <section className="panel errorMonitorPanel">
   <div className="panelHead">
     <div><span className="eyebrow">ERROR MONITOR</span><h2>Application errors</h2><p>Every failed API activity is retained with outlet, user, request and correlation context.</p></div>
     <div className="errorMonitorBadge">{monitor.summary?.unresolvedCount??0} unresolved</div>
   </div>
   {error&&<div className="errorBanner">⚠ {error}<button type="button" onClick={()=>setError('')}>×</button></div>}
   <div className="diagnosticsSettings card">
     <div><span className="eyebrow">REQUEST DIAGNOSTICS</span><h3>Logging policy</h3><p>Applies to all API routes and all web apps using the shared API client. Request logs exclude request bodies, query strings and credentials.</p></div>
     <div className="diagnosticsSettingsControls">
       <label><input type="checkbox" checked={diagnostics.requestLoggingEnabled} onChange={e=>setDiagnostics(v=>({...v,requestLoggingEnabled:e.target.checked}))}/> Enable request logs</label>
       <label><input type="checkbox" checked={diagnostics.detailedLoggingEnabled} onChange={e=>setDiagnostics(v=>({...v,detailedLoggingEnabled:e.target.checked}))}/> Enable detailed exception logs</label>
       <label>Slow request threshold (ms)<input type="number" min="100" max="120000" step="100" value={diagnostics.slowRequestThresholdMs} onChange={e=>setDiagnostics(v=>({...v,slowRequestThresholdMs:Number(e.target.value)}))}/></label>
       <button type="button" className="errorRefreshBtn" disabled={settingsBusy||diagnostics.slowRequestThresholdMs<100||diagnostics.slowRequestThresholdMs>120000} onClick={saveDiagnostics}>{settingsBusy?'Saving…':'Save logging policy'}</button>
     </div>
     {settingsMessage&&<p className="diagnosticsSettingsMessage" role="status">{settingsMessage}</p>}
   </div>
   <div className="errorSummaryStats">
     <div><span>Matching</span><b>{monitor.summary?.totalCount??0}</b></div>
     <div><span>Last 24 hours</span><b>{monitor.summary?.last24HoursCount??0}</b></div>
     <div><span>Unresolved</span><b>{monitor.summary?.unresolvedCount??0}</b></div>
   </div>
   <div className="errorMonitorFilters">
     <label><span>Outlet</span><select value={filters.outletId} onChange={e=>apply({...filters,outletId:e.target.value})}><option value="">All outlets</option>{outlets.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
     <label><span>Severity</span><select value={filters.severity} onChange={e=>apply({...filters,severity:e.target.value})}><option value="">All</option><option value="Error">Error</option><option value="Warning">Warning</option></select></label>
     <label><span>Status</span><select value={filters.resolved} onChange={e=>apply({...filters,resolved:e.target.value})}><option value="">All</option><option value="false">Unresolved</option><option value="true">Resolved</option></select></label>
     <label><span>HTTP</span><select value={filters.statusCode} onChange={e=>apply({...filters,statusCode:e.target.value})}><option value="">All</option><option value="400">400</option><option value="401">401</option><option value="403">403</option><option value="404">404</option><option value="409">409</option><option value="429">429</option><option value="500">500</option><option value="502">502</option><option value="503">503</option></select></label>
     <label className="errorSearch"><span>Search</span><input value={filters.search} onChange={e=>setFilters({...filters,search:e.target.value})} onKeyDown={e=>{if(e.key==='Enter')apply(filters)}} placeholder="Activity, message, SQL, correlation id"/></label>
     <button type="button" className="errorRefreshBtn" onClick={()=>load()} disabled={loading}>↻ Refresh</button>
   </div>
   <div className="errorOutletSummary">
     {(monitor.summary?.byOutlet||[]).map(x=><button type="button" key={x.outletId||'platform'} className={filters.outletId===String(x.outletId||'')?'active':''} onClick={()=>apply({...filters,outletId:x.outletId||''})}><span>{x.outletName}</span><b>{x.errorCount}</b><small>{x.unresolvedCount} unresolved</small></button>)}
     {!(monitor.summary?.byOutlet||[]).length&&<span className="errorNoSummary">No matching outlet activity.</span>}
   </div>
   <div className="errorTable">
     <div className="errorTableHead"><span>Time</span><span>Outlet</span><span>Activity</span><span>Status</span><span>Message</span><span>State</span></div>
     {(monitor.items||[]).map(x=><button type="button" className={'errorTableRow '+(x.severity==='Error'?'errorRowCritical':'')} key={x.id} onClick={()=>open(x.id)}>
       <span>{new Date(x.occurredAtUtc).toLocaleString()}</span>
       <span><b>{x.outletName}</b><small>{x.tenantSlug||'platform'}</small></span>
       <span><b>{x.httpMethod} {x.requestPath}</b><small>{x.activity}</small></span>
       <span className={'errorStatus '+(x.statusCode>=500?'server':'client')}>{x.statusCode}</span>
       <span><b>{x.errorCode}</b><small>{x.message}</small></span>
       <span className={x.isResolved?'errorResolved':'errorOpen'}>{x.isResolved?'Resolved':'Open'}</span>
     </button>)}
     {!(monitor.items||[]).length&&<div className="empty">No errors match the current filters.</div>}
   </div>
   <div className="financePager errorPager"><label>Rows per page <select value={pageSize} onChange={e=>{setPageSize(Number(e.target.value));setPage(1)}}><option value={10}>10</option><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option><option value={200}>200</option></select></label><span>{monitor.totalCount?((page-1)*pageSize+1)+'–'+Math.min(page*pageSize,monitor.totalCount)+' of '+monitor.totalCount:'0 results'}</span><div><button type="button" className="errorRefreshBtn" disabled={loading||page<=1} onClick={()=>setPage(p=>Math.max(1,p-1))}>Previous</button><button type="button" className="errorRefreshBtn" disabled={loading||page*pageSize>=(monitor.totalCount||0)} onClick={()=>setPage(p=>p+1)}>Next</button></div></div>

   {selected&&<div className="errorDetailBackdrop"><div className="errorDetailModal">
     <div className="errorDetailHead"><div><span className="eyebrow">ERROR DETAIL</span><h2>{selected.errorCode}</h2><p>{new Date(selected.occurredAtUtc).toLocaleString()} · {selected.severity} · HTTP {selected.statusCode}</p></div><button type="button" onClick={()=>setSelected(null)}>×</button></div>
     <div className="errorDetailGrid">
       <section><h3>Activity</h3><div className="errorFacts"><span>Outlet</span><b>{selected.outletName}</b><span>Tenant</span><b>{selected.tenantSlug||'Platform / system'}</b><span>Request</span><b>{selected.httpMethod} {selected.requestPath}</b><span>Activity</span><b>{selected.activity}</b><span>User</span><b>{selected.userName} · {selected.userRole||'Anonymous'}</b><span>Elapsed</span><b>{selected.elapsedMilliseconds} ms</b><span>Correlation</span><b className="mono">{selected.correlationId}</b><span>Trace</span><b className="mono">{selected.traceId}</b></div></section>
       <section><h3>Exception / response</h3><div className="errorMessageBlock"><b>{selected.exceptionType||'HTTP response'}</b><p>{selected.message}</p>{selected.innerExceptionMessage&&<><strong>Inner exception</strong><p>{selected.innerExceptionMessage}</p></>}</div></section>
       <section className="errorStackSection"><h3>Stack trace</h3><pre>{selected.stackTrace||'No stack trace was recorded for this response.'}</pre></section>
       <section><h3>Request context</h3><div className="errorFacts"><span>Environment</span><b>{selected.environment}</b><span>Host</span><b>{selected.tenantHost||'—'}</b><span>Client IP</span><b>{selected.clientIpAddress||'—'}</b><span>User agent</span><b>{selected.userAgent||'—'}</b><span>Fingerprint</span><b className="mono">{selected.fingerprint}</b></div></section>
       <section className="errorResolution"><h3>Resolution</h3>{selected.isResolved?<div className="resolvedBox"><b>Resolved</b><span>{selected.resolvedByUserName||'Admin'} · {selected.resolvedAtUtc?new Date(selected.resolvedAtUtc).toLocaleString():''}</span><p>{selected.resolutionNotes||'No resolution notes.'}</p></div>:<div className="openBox"><textarea value={resolutionNotes} onChange={e=>setResolutionNotes(e.target.value)} placeholder="What was investigated or fixed?"/><button type="button" onClick={resolve} disabled={loading}>Mark resolved</button></div>}</section>
     </div>
   </div></div>}
 </section>;
}
