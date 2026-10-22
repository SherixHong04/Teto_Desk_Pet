-- Read isolated snapshots; export the native flattened frames unchanged.
local file=assert(io.open(app.params.plan,"rb"))
local plan=json.decode(file:read("*a")); file:close()
for _,entry in ipairs(plan.animations) do
  local sprite=Sprite{fromFile=entry.snapshot}
  assert(sprite.width==192 and sprite.height==208,"Unexpected native canvas")
  app.fs.makeAllDirectories(entry.split)
  local report={name=entry.name,source=entry.source,snapshot=entry.snapshot,
    sourceSha256=entry.sourceSha256,width=sprite.width,height=sprite.height,
    frames=#sprite.frames,durations={},layers={},tags={},spriteData=sprite.data}
  for _,layer in ipairs(sprite.layers) do
    report.layers[#report.layers+1]={name=layer.name,visible=layer.isVisible}
  end
  for _,tag in ipairs(sprite.tags) do
    report.tags[#report.tags+1]={name=tag.name,first=tag.fromFrame.frameNumber,last=tag.toFrame.frameNumber}
  end
  for i,frame in ipairs(sprite.frames) do
    local image=Image(sprite.spec); image:drawSprite(sprite,i)
    image:saveAs(entry.split.."/"..string.format("%03d",i)..".png")
    report.durations[i]=math.floor(frame.duration*1000+0.5)
  end
  local out=assert(io.open(entry.nativeReport,"wb"));out:write(json.encode(report));out:close()
  sprite:close()
  print(entry.name..": "..report.frames.." native frames exported")
end
