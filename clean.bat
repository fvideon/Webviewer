:: Clean output folders

for /D /R %%i in (bin*) do (rd /s /q "%%i")
for /D /R %%i in (obj*) do (rd /s /q "%%i")


