Date.prototype.addHours= function(h){
    this.setHours(this.getHours() + h);
    return this;
}
Date.prototype.addDays = function(days){
    var dat = new Date(this.valueOf());
    dat.setDate(dat.getDate() + days);
    return dat;
};
Date.prototype.ddmmyyyy = function() {
	var mm = this.getMonth() + 1; //January is 0!
	var dd = this.getDate();
	if(dd < 10) dd = '0' + dd;
	if(mm < 10) mm = '0' + mm;
	
	return [dd, mm, this.getFullYear()].join('.');
};
Date.prototype.fullformat = function() { 
	var hours = this.getHours();
	var minutes = this.getMinutes();
	hours = hours < 10 ? '0'+hours : hours;
	minutes = minutes < 10 ? '0'+minutes : minutes;
	var strTime = hours + ':' + minutes;
	var day = this.getDate();
	day = day < 10 ? '0'+day : day;
	return day + "/" + (this.getMonth()+1) + "/" + this.getFullYear() + "  " + strTime;
};
String.prototype.format = String.prototype.f = function() {
    var s = this,
        i = arguments.length;

    while (i--) {
        s = s.replace(new RegExp('\\{' + i + '\\}', 'gm'), arguments[i]);
    }
    return s;
};

var std = 10; // делим ширину машин\заказов на эту константу
var hourPX = 30; // такой же как и line-height в классе .scale

$(document).ready(function(){
			
	var waste = 0;
	var readjustments = 0;
	var deadline = 0;
	
	for( var i = 0; i < data.Machines.length; i++ )
	{
		var m = data.Machines[i];
		var domM = new Machine(m);
		waste += m.WasteVolume;
		readjustments += m.ReadjustmentHours;
		deadline += m.DeadlineHours;
	}
	
	$totalBlock = $(".aggregates");
	var totalTemplateHTML = 
	("<div class='row machine-stats'>" +
		"<div>" +
			"<div class='name'><p>{0}</p></div>" +
			"<div class='numbers'>" +
				"<div><p>Отходы</p><p>{1}</p></div>" +
				"<div><p>Переналадки(ч.)</p><p>{2}</p></div>" +
				"<div><p>Просрочено(ч.)</p><p>{3}</p></div>" +
			"</div>" +
		"</div>" +
	"</div>").format( "Итого", waste, readjustments, deadline );
	
	$totalBlock.append(totalTemplateHTML);
	
});



function Machine( m_data ) {
	
	this.data = m_data;
	this.timelineStart = new Date(m_data.TimelineStart);
	this.timelineEnd = new Date(m_data.TimelineEnd);
	
	var id = m_data.Id;
	
	/*-----------------------------------------------------------------------*/
	var containerTemplateHTML =
	("<div id='machine-{0}' class='machine-container'>" +
		"<div class='scale'>" +
		"</div>" +
	"</div>").format(id);
	
	$container = $(containerTemplateHTML);
	$('#machines').append($container);
	
	var dayTemplateHtml =
	"<div class='half-day'>" +
		"<div class='quorter-day'>" +
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>1</p></div>" +
				"<div class='hour'><p>2</p></div>" +
				"<p>3</p>" +
			"</div>" + 
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>4</p></div>" +
				"<div class='hour'><p>5</p></div>" +
			"</div>" +
			"<p>6</p>" +
		"</div>" +
		"<div class='quorter-day'>" +
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>7</p></div>" +
				"<div class='hour'><p>8</p></div>" +
				"<p>9</p>" +
			"</div>" + 
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>10</p></div>" +
				"<div class='hour'><p>11</p></div>" +
			"</div>" +
		"</div>" +		
		"<p>12</p>" +
	"</div>" +
	"<div class='half-day'>" +
		"<div class='quorter-day'>" +
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>13</p></div>" +
				"<div class='hour'><p>14</p></div>" +
				"<p>15</p>" +
			"</div>" + 
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>16</p></div>" +
				"<div class='hour'><p>17</p></div>" +
			"</div>" +
			"<p>18</p>" +
		"</div>" +
		"<div class='quorter-day'>" +
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>19</p></div>" +
				"<div class='hour'><p>20</p></div>" +
				"<p>21</p>" +
			"</div>" + 
			"<div class='quorter-half-day'>" +
				"<div class='hour'><p>22</p></div>" +
				"<div class='hour'><p>23</p></div>" +
			"</div>" +
		"</div>" +		
	"</div>";
	
	
	var currentTime = this.timelineStart;
	var scaleLength = Math.round((this.timelineEnd - this.timelineStart)/(1000*60*60*24)) + 1;
	
	function timeBlock(cssclass, number){
		return "<div class='" + cssclass + "'><p>" + number + "</p></div>";
	}
	
	var $scale = $container.find(".scale");
	
	for (var i = 0; i < scaleLength; i++)
	{
		var dayTime = currentTime.addDays(i);
		var dayObj = $(timeBlock('day', dayTime.ddmmyyyy()));
		dayObj.append(dayTemplateHtml);
		$scale.append(dayObj);
		
		if ((i + 1) == scaleLength)
		{
			$scale.append($(timeBlock('day', dayTime.addDays(1).ddmmyyyy())));
		}
	}
	/*-----------------------------------------------------------------------*/
		
	this.draw = SVG('machine-' + id).size(m_data.StripWidth/std, $scale.height());
	
	var that = this;
	var orderHover = function() {
		var id = this.attr('orderId');
		var order = that.data.Orders[id];
		
		var id = order.Id;
		var type = order.Type;
		var consumer = order.Consumer;
		var productName = order.ProductName;
		
		var start = new Date(order.Start);
		var end = new Date(order.End);
		var deadline = new Date(order.Deadline);
		
		var density = order.Density;
		var volume = order.Volume;
		var width = order.Width;
		
		var template = "<div><p>{0}</p><p>{1}</p></div>";
		
		var finalTemplate = template.format("ID", id);
		//finalTemplate += template.format("TYPE",type);
		finalTemplate += template.format("Заказчик",consumer);
		finalTemplate += template.format("Номенклатура",productName);
		finalTemplate += template.format("Выпуск с",start.fullformat());
		finalTemplate += template.format("Выпуск до",end.fullformat());
		finalTemplate += template.format("Срок",deadline.fullformat());
		finalTemplate += template.format("Граммаж",density);
		finalTemplate += template.format("Объём",volume);
		finalTemplate += template.format("Формат",width);
		
		$(".details").html(finalTemplate);
		this.stroke({ color: '#441414' });
	}
	
	for( var j = 0; j < m_data.Orders.length; j++ )	{
		var ord = m_data.Orders[j];
		var startTime = new Date(ord.Start);
		var endTime = new Date(ord.End);
		
		var offsetY = (startTime - this.timelineStart)/(1000*60*60) * hourPX;
		var sizeY = (endTime - startTime)/(1000*60*60) * hourPX;
		
		var stroke = 2;
		
		if ( sizeY < 0 )
			console.log("WTF");
		
		if ( sizeY > stroke ) sizeY -= stroke;
		// stroke 4 рисует 2 пикселя внутрь + 2 наружу => вычитаем из размеров и сдвигаем
		var rect = this.draw.rect(ord.Width/std - stroke, sizeY).move(ord.OffsetX/std + stroke/2, offsetY + stroke/2);
		/*
		Order = 1, rgb(196,189,151) rgb(127,127,127) d8cfa0
        Waste = 2, rgb(127,127,127)
        Readjustment = 3,
        Maintenance = 4 
		*/
		switch( ord.Type ) {
			case 1: rect.attr({ fill: '#d8cfa0' }).stroke({ color: '#7f829e', width: stroke }); break; 
			case 2: rect.attr({ fill: 'rgba(255,0,0,0.1)' }).stroke({ color: '#7f829e', width: stroke }); break;
			case 3: rect.attr({ fill: '#646682' }).stroke({ color: '#f0f0f0', width: stroke }); break;
			case 4: break;
		}
		rect.attr({ orderId: j });
		
		rect.mouseover(orderHover);
		rect.mouseout(function() { 
			$(".details").empty(); 
			var id = this.attr('orderId');
			var order = that.data.Orders[id];
			if ( order.Type == 3 )
				this.stroke({ color: '#f0f0f0' }); 
			else
				this.stroke({ color: '#7f829e' }); 
		});
	}
	
	/*-----------------------------------------------------------------------*/	
	$stats = $("#stats");
	var statsTemplateHTML = 
	("<div class='row machine-stats' style='width:{0}px;'>" +
		"<div>" +
			"<div class='name'><p>{1}</p></div>" +
			"<div class='numbers'>" +
				"<div><p>Отходы</p><p>{2}</p></div>" +
				"<div><p>Переналадки(ч.)</p><p>{3}</p></div>" +
				"<div><p>Просрочено(ч.)</p><p>{4}</p></div>" +
			"</div>" +
		"</div>" +
	"</div>").format(
		$container.width(), m_data.Name, m_data.WasteVolume,
		m_data.ReadjustmentHours, m_data.DeadlineHours
	);
	
	$stats.append(statsTemplateHTML);
	
}
