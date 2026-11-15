// Generated from assets/notebooks/appearance.json.
package app.moji.android.ui
import androidx.compose.ui.graphics.vector.ImageVector
import app.moji.android.data.Notebook
import app.moji.android.data.NoteMode
data class NotebookPalette(val name:String,val start:String,val end:String="")
val notebookIcons=listOf("Book" to "笔记本","Document" to "文档","Prompt" to "灵感","Lock" to "钥匙","Star" to "星标","Archive" to "收纳","Pin" to "图钉","Image" to "图片","Cloud" to "云朵","Code" to "代码","Search" to "搜索","Settings" to "齿轮","Heart" to "心愿","Leaf" to "绿叶","Flower" to "花朵","Sun" to "太阳","Moon" to "月亮","Mountain" to "山野","Plane" to "旅行","Compass" to "指南针","Globe" to "世界","Camera" to "摄影","Music" to "音乐","Film" to "电影","Palette" to "绘画","Pen" to "写作","Lightbulb" to "想法","Graduation" to "学习","Briefcase" to "工作","Calendar" to "日程","Checklist" to "清单","Coffee" to "咖啡","Gift" to "礼物","Wallet" to "账本","Target" to "目标","Terminal" to "开发")
val notebookSolids=listOf(NotebookPalette("鼠尾草","#75876D",""),NotebookPalette("麦穗","#B2946E",""),NotebookPalette("雾蓝","#8393AC",""),NotebookPalette("烟粉","#A5849D",""),NotebookPalette("陶土","#BA886C",""),NotebookPalette("薰衣草","#9182A8",""),NotebookPalette("松针","#557768",""),NotebookPalette("青瓷","#6C9B95",""),NotebookPalette("海盐","#7198B1",""),NotebookPalette("牛仔","#60718D",""),NotebookPalette("葡萄","#7C678E",""),NotebookPalette("玫瑰","#B8798B",""),NotebookPalette("杏桃","#D6A285",""),NotebookPalette("蜂蜜","#C8AC65",""),NotebookPalette("燕麦","#ADAA96",""),NotebookPalette("岩灰","#7F8991",""))
val notebookGradients=listOf(NotebookPalette("冬日晴空","#A1C4FD","#C2E9FB"),NotebookPalette("桃色微光","#FF9A9E","#FAD0C4"),NotebookPalette("杏子奶油","#FFECD2","#FCB69F"),NotebookPalette("薰衣草雾","#E0C3FC","#8EC5FC"),NotebookPalette("樱花薄雾","#FBC2EB","#A6C1EE"),NotebookPalette("春日新芽","#D4FC79","#96E6A1"),NotebookPalette("晨间暖阳","#F6D365","#FDA085"),NotebookPalette("纸页时光","#FDFCFB","#E2D1C3"),NotebookPalette("苔绿花园","#CFE0BE","#B7D4CC"),NotebookPalette("海岸信笺","#A8D5D1","#D5DEEF"),NotebookPalette("落日余温","#D9AEC2","#EAC9A7"),NotebookPalette("灰紫云层","#D3CCE3","#E9E4F0"))
fun notebookIconId(book:Notebook):String=book.icon.takeIf{icon->notebookIcons.any{it.first==icon}}?:when(book.mode){NoteMode.Text->"Document";NoteMode.Prompt->"Prompt";NoteMode.Password->"Lock"}
fun notebookIcon(book:Notebook):ImageVector=when(notebookIconId(book)){
"Book"->MojiIcons.Notebook
"Document"->MojiIcons.Description
"Prompt"->MojiIcons.AutoAwesome
"Lock"->MojiIcons.Key
"Star"->MojiIcons.Star
"Archive"->MojiIcons.Archive
"Pin"->MojiIcons.PushPin
"Image"->MojiIcons.Image
"Cloud"->MojiIcons.CloudDone
"Code"->MojiIcons.Code
"Search"->MojiIcons.Search
"Settings"->MojiIcons.Settings
"Heart"->MojiIcons.Heart
"Leaf"->MojiIcons.Leaf
"Flower"->MojiIcons.Flower
"Sun"->MojiIcons.Sun
"Moon"->MojiIcons.Moon
"Mountain"->MojiIcons.Mountain
"Plane"->MojiIcons.Plane
"Compass"->MojiIcons.Compass
"Globe"->MojiIcons.Globe
"Camera"->MojiIcons.Camera
"Music"->MojiIcons.Music
"Film"->MojiIcons.Film
"Palette"->MojiIcons.Palette
"Pen"->MojiIcons.Pen
"Lightbulb"->MojiIcons.Lightbulb
"Graduation"->MojiIcons.Graduation
"Briefcase"->MojiIcons.Briefcase
"Calendar"->MojiIcons.Calendar
"Checklist"->MojiIcons.Checklist
"Coffee"->MojiIcons.Coffee
"Gift"->MojiIcons.Gift
"Wallet"->MojiIcons.Wallet
"Target"->MojiIcons.Target
"Terminal"->MojiIcons.Terminal
else->modeIcon(book.mode)
}
