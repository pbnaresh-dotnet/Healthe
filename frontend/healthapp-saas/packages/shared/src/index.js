 if(typeof File==='undefined'||!(file instanceof File)||!String(file.type||'').toLowerCase().startsWith('image/')) return file;
 if(typeof document==='undefined'||typeof createImageBitmap==='undefined') return file;
 const maxByKind={recipe:2200,hero:2400,logo:512,favicon:256,avatar:1024,general:2200};
 const max=maxByKind[kind]||maxByKind.general;
 try{
   const bitmap=await createImageBitmap(file);
   const largest=Math.max(bitmap.width,bitmap.height);
   const scale=Math.min(1,max/largest);
   const width=Math.max(1,Math.round(bitmap.width*scale));
   const height=Math.max(1,Math.round(bitmap.height*scale));
   if(scale===1&&file.size<=750*1024&&file.type==='image/webp'){bitmap.close();return file;}