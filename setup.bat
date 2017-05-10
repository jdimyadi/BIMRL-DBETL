rm -rf C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins
md C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins
md C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETL.XplorerPlugin
md C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETLConfig.XplorerPlugin
md C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_Main.XplorerPlugin

cd BIMRL_ETL.XplorerPlugin\bin\x64\ReleaseWithPDB\
cp -rf * C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETL.XplorerPlugin\

cd ..\..\..\..\BIMRL_ETLConfig.XplorerPlugin\bin\x64\ReleaseWithPDB\
cp -rf * C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETLConfig.XplorerPlugin\

cd ..\..\..\..\BIMRL_Main.XplorerPlugin\bin\x64\ReleaseWithPDB\
cp -rf * C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_Main.XplorerPlugin\

cd ..\..\..\..\
cp -rf script C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\
cp -rf PluginConfig.xml C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETL.XplorerPlugin
cp -rf PluginConfig.xml C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_ETLConfig.XplorerPlugin
cp -rf PluginConfig.xml C:\Users\wsoli\Documents\GitHub\Xbim-Invicara\XbimWindowsUI\Output\Release\Plugins\BIMRL_Main.XplorerPlugin

